using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Profiling;

// 仅显式验收参数挂载；正常游戏没有组件、逐帧检查或采样开销。
// 输入是本地私有权威夹具；输出只有设备/性能/渲染统计，绝不复制存档和手牌。
public sealed class UltraBaselineProbe : MonoBehaviour
{
    [Serializable] sealed class Device
    {
        public string build, unity, os, cpu, gpu, graphicsApi, graphicsDriver, quality, pipeline, launchMode;
        public int cpuThreads, systemMemoryMb, gpuMemoryMb, width, height, targetFrameRate, vSyncCount, msaa;
        public float renderScale, shadowDistance;
        public bool developmentBuild, frameTimingFeatureEnabled, runInBackground;
    }
    [Serializable] sealed class Census
    {
        public int activeRenderers, activeMaterialSlots, distinctActiveMaterials, residentMaterials, residentTextures, residentMeshes;
        public long residentTextureRuntimeBytes;
        public string note = "采样开始前枚举；纹理/网格含Unity与UI当前驻留资源；RuntimeMemorySize不是显存精确占用。";
    }
    struct Pose { public Vector3 position; public Quaternion rotation; public float size; }
    struct Sample
    {
        public double elapsed, milliseconds, cpu, gpu;
        public long allocated, reserved, mono, gcBytes, draws, batches, triangles;
        public int generation0, generation1, generation2, renderedFrames;
        public bool focused;
    }
    readonly FrameTiming[] timing = new FrameTiming[1];
    readonly string[] scenarios = { "ordinary", "maximum", "crowded", "fog" };
    M6App app;
    TabletopScene table;
    Camera camera;
    string output, fixtures, stage = "初始化", sampleOperation = "";
    int sampledFrames, sampledRenderedFrames;
    bool visibleWindow, captureOnly;
    float sampleSeconds, warmupSeconds;
    int renderedFrames;
    ProfilerRecorder gcRecorder, drawRecorder, batchRecorder, triangleRecorder;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--ultra-baseline-verify") < 0) return;
        new GameObject("U0 显式性能基线采样").AddComponent<UltraBaselineProbe>();
    }
    void Start()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            output = Path.GetFullPath(Argument(args, "--ultra-baseline-verify"));
            fixtures = Path.GetFullPath(Argument(args, "--ultra-baseline-fixtures"));
            sampleSeconds = float.Parse(Argument(args, "--ultra-baseline-seconds", "30"), Invariant);
            warmupSeconds = float.Parse(Argument(args, "--ultra-baseline-warmup", "8"), Invariant);
            visibleWindow = Array.IndexOf(args, "--ultra-baseline-visible-window") >= 0;
            captureOnly = Array.IndexOf(args, "--ultra-baseline-capture-only") >= 0;
            Require(sampleSeconds >= 3 && sampleSeconds <= 600 && warmupSeconds >= 2, "无效采样时长。");
            Require(!Application.isEditor && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "必须使用启用图形的独立游戏。");
            Directory.CreateDirectory(output);
            Application.runInBackground = true;
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            StartCoroutine(GuardedRun());
        }
        catch (Exception ex) { Fail(ex); }
    }
    static string Argument(string[] args, string name, string fallback = null)
    {
        int index = Array.IndexOf(args, name);
        if (index < 0 && fallback != null) return fallback;
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("缺少采样参数：" + name);
        return args[index + 1];
    }
    IEnumerator GuardedRun()
    {
        var work = Run();
        while (true)
        {
            object current;
            try { if (!work.MoveNext()) break; current = work.Current; }
            catch (Exception ex) { Fail(ex); yield break; }
            yield return current;
        }
        File.WriteAllText(Path.Combine(output, "result.json"), "{\"status\":" + Quote(captureOnly ? "captured-only" : "passed") +
            ",\"performanceMeasured\":" + Bool(!captureOnly) + ",\"scenarios\":4,\"authorityUnchanged\":true,\"screenshotsPerScenario\":4,\"androidDeviceVerified\":false}", new UTF8Encoding(false));
        Debug.Log(captureOnly
            ? "CATAN_ULTRA_BASELINE_CAPTURE_ONLY_OK: four fixtures; sixteen explicit URP captures; memory snapshots; no performance measurement"
            : "CATAN_ULTRA_BASELINE_OK: four fixtures; fixed camera route; raw frame samples; authority unchanged");
        Application.Quit(0);
    }
    IEnumerator Run()
    {
        yield return null;
        app = FindFirstObjectByType<M6App>();
        Require(app != null, "未找到实际游戏入口。");
        // 固定分辨率由启动脚本设置；不要在测量时调整显示设置。
        foreach (string scenario in scenarios)
        {
            stage = scenario + "：恢复夹具";
            app.ReadSave(Path.Combine(fixtures, scenario + ".json"));
            yield return null; yield return null;
            Require(app.Ready && app.Paused && !app.NetworkGame && app.Fault == "", "夹具未以暂停本地游戏恢复。");
            string captureAuthority = captureOnly ? app.ExportEnvelope() : null;
            table = FindFirstObjectByType<TabletopScene>(); camera = table != null ? table.TableCamera : null;
            Require(table != null && camera != null && camera.enabled, "实际棋盘相机不可用。");
            Require(FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.enabled && r.gameObject.activeInHierarchy) > 20, "没有足够的可渲染棋盘对象。");
            var poses = BuildRoute();
            string scenarioDirectory = Path.Combine(output, scenario);
            Directory.CreateDirectory(scenarioDirectory);
            // 截图和资源加载均在预热之前，避免把同步读回时间混入帧时间。
            for (int i = 0; i < 4; i++)
            {
                stage = scenario + "：相机截图" + i;
                ApplyPose(poses[i]);
                yield return null;
                Capture(Path.Combine(scenarioDirectory, "camera-" + i + ".png"));
            }
            ApplyPose(poses[0]);
            yield return null; // 截图临时纹理经过 Destroy 后再统计驻留资源。
            stage = scenario + "：预热";
            double warmup = Clock();
            while (Clock() - warmup < warmupSeconds) yield return null;
            Require(app.Paused, "预热阶段意外恢复 AI。");
            var census = TakeCensus();
            if (captureOnly)
            {
                stage = scenario + "：仅截图状态检查";
                Require(captureAuthority == app.ExportEnvelope(), "截图和预热期间权威状态发生变化。");
                WriteCaptureResults(scenarioDirectory, scenario, census);
                Debug.Log("CATAN_ULTRA_BASELINE_CAPTURE_SCENARIO_OK: " + scenario + "; screenshots=4; performanceMeasured=false");
                continue;
            }
            string before = app.ExportEnvelope();
            var samples = new Sample[Math.Min(1200000, Mathf.CeilToInt(sampleSeconds * 2000) + 10)];
            gcRecorder = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            drawRecorder = StartRecorder(ProfilerCategory.Render, "Draw Calls Count");
            batchRecorder = StartRecorder(ProfilerCategory.Render, "Batches Count");
            triangleRecorder = StartRecorder(ProfilerCategory.Render, "Triangles Count");
            int count = 0, renderStart = renderedFrames;
            int[] gcStart = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            long processStart = WorkingSet();
            ulong lastTiming = 0;
            // 分配缓冲区和启动 Recorder 的时间不算入采样。
            yield return null;
            double start = Clock(), previous = start;
            stage = scenario + "：逐帧采样";
            sampledFrames = 0; sampledRenderedFrames = 0; sampleOperation = "FrameTimingManager.CaptureFrameTimings";
            FrameTimingManager.CaptureFrameTimings();
            while (true)
            {
                yield return null;
                double now = Clock();
                sampleOperation = "样本容量检查";
                if (count >= samples.Length) throw new InvalidOperationException("采样容量不足，拒绝静默截断。");
                double cpu = double.NaN, gpu = double.NaN;
                sampleOperation = "FrameTimingManager.GetLatestTimings";
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != lastTiming)
                {
                    lastTiming = timing[0].frameStartTimestamp;
                    if (timing[0].cpuFrameTime > 0) cpu = timing[0].cpuFrameTime;
                    if (timing[0].gpuFrameTime > 0) gpu = timing[0].gpuFrameTime;
                }
                sampleOperation = "ProfilerRecorder.GC";
                long gcValue = Value(gcRecorder);
                sampleOperation = "ProfilerRecorder.DrawCalls";
                long drawValue = Value(drawRecorder);
                sampleOperation = "ProfilerRecorder.Batches";
                long batchValue = Value(batchRecorder);
                sampleOperation = "ProfilerRecorder.Triangles";
                long triangleValue = Value(triangleRecorder);
                sampleOperation = "内存计数器";
                samples[count++] = new Sample
                {
                    elapsed = now - start, milliseconds = (now - previous) * 1000,
                    cpu = cpu, gpu = gpu, allocated = Profiler.GetTotalAllocatedMemoryLong(),
                    reserved = Profiler.GetTotalReservedMemoryLong(), mono = Profiler.GetMonoUsedSizeLong(),
                    gcBytes = gcValue, draws = drawValue, batches = batchValue, triangles = triangleValue,
                    generation0 = GC.CollectionCount(0), generation1 = GC.CollectionCount(1), generation2 = GC.CollectionCount(2),
                    focused = Application.isFocused, renderedFrames = renderedFrames - renderStart
                };
                sampledFrames = count;
                sampledRenderedFrames = renderedFrames - renderStart;
                // 最小化/隐藏窗口可能高速更新却跳过渲染；这不是游戏帧率，不能扩容后继续当作性能结果。
                sampleOperation = "持续渲染检查";
                if (count == 100 && sampledRenderedFrames < 50)
                    throw new InvalidOperationException("前100次采样未持续渲染，拒绝将空转更新当作游戏帧率。");
                previous = now;
                if (now - start >= sampleSeconds) break;
                sampleOperation = "镜头路线";
                ApplyRoute(poses, (float)((now - start) / sampleSeconds));
                sampleOperation = "FrameTimingManager.CaptureFrameTimings";
                FrameTimingManager.CaptureFrameTimings();
            }
            int[] gcEnd = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            long processEnd = WorkingSet();
            StopRecorders();
            stage = scenario + "：状态与渲染检查";
            Require(app.Paused && before == app.ExportEnvelope(), "采样期间权威状态发生变化。");
            Require(renderedFrames - renderStart >= count * .9, "采样期间相机没有持续渲染；后台结果无效。");
            WriteResults(scenarioDirectory, scenario, samples, count, gcStart, gcEnd, processStart, processEnd, census);
            samples = null;
            Debug.Log("CATAN_ULTRA_BASELINE_SCENARIO_OK: " + scenario + "; frames=" + count);
        }
    }
    Pose[] BuildRoute()
    {
        var poses = new Pose[5];
        table.ResetPan(); table.Frame(camera, false, 1); poses[0] = ReadPose();
        table.Frame(camera, true, 1); poses[1] = ReadPose();
        table.Frame(camera, false, .55f); poses[2] = ReadPose();
        poses[3] = poses[2]; poses[3].position += Vector3.right * poses[2].size * .8f;
        poses[4] = poses[0];
        return poses;
    }
    Pose ReadPose() => new Pose { position = camera.transform.position, rotation = camera.transform.rotation, size = camera.orthographicSize };
    void ApplyPose(Pose pose) { camera.transform.SetPositionAndRotation(pose.position, pose.rotation); camera.orthographicSize = pose.size; }
    void ApplyRoute(Pose[] poses, float fraction)
    {
        float position = Mathf.Clamp01(fraction) * 4;
        int segment = Mathf.Min(3, (int)position);
        float blend = Mathf.SmoothStep(0, 1, position - segment);
        ApplyPose(new Pose { position = Vector3.Lerp(poses[segment].position, poses[segment + 1].position, blend),
            rotation = Quaternion.Slerp(poses[segment].rotation, poses[segment + 1].rotation, blend),
            size = Mathf.Lerp(poses[segment].size, poses[segment + 1].size, blend) });
    }
    void OnCameraRendered(ScriptableRenderContext context, Camera rendered) { if (rendered == camera) renderedFrames++; }
    void Capture(string path)
    {
        var previous = RenderTexture.active;
        var target = new RenderTexture(Screen.width, Screen.height, 24);
        var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        try
        {
            target.Create();
            // 与已有资产验收使用相同的显式 URP 请求。此图只证明棋盘渲染，不包含 IMGUI。
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            Require(RenderPipeline.SupportsRenderRequest(camera, request), "当前 URP 不支持相机采集。");
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); image.Apply();
            var pixels = image.GetPixels32();
            var first = pixels[0];
            int different = 0, bright = 0;
            foreach (var pixel in pixels)
            {
                if (Math.Abs(pixel.r - first.r) + Math.Abs(pixel.g - first.g) + Math.Abs(pixel.b - first.b) > 30) different++;
                if (pixel.r + pixel.g + pixel.b > 60) bright++;
            }
            Require(different > pixels.Length * .05 && bright > pixels.Length * .2, "相机截图为黑屏或纯色，不可作为证据。");
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(image); }
    }
    static Census TakeCensus()
    {
        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).ToArray();
        var textures = Resources.FindObjectsOfTypeAll<Texture>();
        return new Census
        {
            activeRenderers = renderers.Length, activeMaterialSlots = materials.Length,
            distinctActiveMaterials = materials.Distinct().Count(), residentMaterials = Resources.FindObjectsOfTypeAll<Material>().Length,
            residentTextures = textures.Length, residentMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Length,
            residentTextureRuntimeBytes = textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t))
        };
    }
    void WriteResults(string path, string scenario, Sample[] buffer, int count, int[] gcStart, int[] gcEnd, long processStart, long processEnd, Census census)
    {
        var samples = buffer.Take(count).ToArray();
        int gcValidSamples = samples.Count(s => s.gcBytes >= 0);
        using (var csv = new StreamWriter(Path.Combine(path, "frames.csv"), false, new UTF8Encoding(false)))
        {
            csv.WriteLine("frame,elapsedSeconds,frameMs,cpuMs,gpuMs,allocatedBytes,reservedBytes,monoUsedBytes,gcAllocatedBytes,gc0,gc1,gc2,drawCalls,batches,triangles,focused,renderedFrames");
            for (int i = 0; i < samples.Length; i++)
            {
                var s = samples[i];
                csv.WriteLine(i + "," + Num(s.elapsed) + "," + Num(s.milliseconds) + "," + CsvNumber(s.cpu) + "," + CsvNumber(s.gpu) + "," +
                    s.allocated + "," + s.reserved + "," + s.mono + "," + CsvCounter(s.gcBytes) + "," + s.generation0 + "," + s.generation1 + "," + s.generation2 + "," +
                    CsvCounter(s.draws) + "," + CsvCounter(s.batches) + "," + CsvCounter(s.triangles) + "," + (s.focused ? 1 : 0) + "," + s.renderedFrames);
            }
        }
        var device = DescribeDevice();
        string json = "{\n\"schema\":\"ultra-baseline-performance-v1\",\"scenario\":" + Quote(scenario) +
            ",\"theme\":\"真实桌游\",\"device\":" + JsonUtility.ToJson(device, true) +
            ",\"sampleSeconds\":" + Num(samples.Last().elapsed) + ",\"warmupSeconds\":" + Num(warmupSeconds) +
            ",\"sampleCount\":" + count + ",\"focusedFrames\":" + samples.Count(s => s.focused) +
            ",\"renderedFrames\":" + samples.Last().renderedFrames + ",\"sceneCensus\":" + JsonUtility.ToJson(census) +
            ",\"authorityUnchanged\":true,\"route\":\"v1：0%-25%全桌斜视到高角；25%-50%到0.55近景；50%-75%右移0.8近景半高；75%-100%回全桌。四段SmoothStep，相机在公开棋盘中心拟合；采样不执行UI输入。\"" +
            ",\"frameMs\":" + Distribution(samples.Select(s => s.milliseconds)) +
            ",\"cpuMs\":" + Distribution(samples.Select(s => s.cpu)) + ",\"gpuMs\":" + Distribution(samples.Select(s => s.gpu)) +
            ",\"cpuTimingCoverage\":" + Num((double)samples.Count(s => !double.IsNaN(s.cpu)) / count) +
            ",\"gpuTimingCoverage\":" + Num((double)samples.Count(s => !double.IsNaN(s.gpu)) / count) +
            ",\"frameTimingNote\":\"CPU/GPU只记FrameTimingManager返回的正数和不同时间戳；无有效样本时分位数为null。其采样可能延后一帧，不能代替GPU profiler。\"" +
            ",\"spikesOver50Ms\":" + samples.Count(s => s.milliseconds > 50) +
            ",\"gcRecorderAvailable\":" + Bool(gcValidSamples > 0) + ",\"renderCountersAvailable\":" + Bool(samples.Any(s => s.draws >= 0)) +
            ",\"gcValidSamples\":" + gcValidSamples + ",\"gcCounterCoverage\":" + Num((double)gcValidSamples / count) +
            ",\"gcCollections\":[" + (gcEnd[0] - gcStart[0]) + "," + (gcEnd[1] - gcStart[1]) + "," + (gcEnd[2] - gcStart[2]) + "]" +
            ",\"gcAllocatedBytesTotal\":" + (gcValidSamples == count ? samples.Sum(s => s.gcBytes).ToString(Invariant) : "null") +
            ",\"gcAllocatedBytesObserved\":" + (gcValidSamples > 0 ? samples.Sum(s => Math.Max(0, s.gcBytes)).ToString(Invariant) : "null") +
            ",\"allocatedPeakBytes\":" + samples.Max(s => s.allocated) + ",\"reservedPeakBytes\":" + samples.Max(s => s.reserved) +
            ",\"monoUsedPeakBytes\":" + samples.Max(s => s.mono) + ",\"allocatedStartBytes\":" + samples[0].allocated +
            ",\"allocatedEndBytes\":" + samples.Last().allocated + ",\"processWorkingSetStartBytes\":" + Counter(processStart) +
            ",\"processWorkingSetEndBytes\":" + Counter(processEnd) +
            ",\"drawCalls\":" + Distribution(samples.Select(s => s.draws < 0 ? double.NaN : (double)s.draws)) +
            ",\"batches\":" + Distribution(samples.Select(s => s.batches < 0 ? double.NaN : (double)s.batches)) +
            ",\"triangles\":" + Distribution(samples.Select(s => s.triangles < 0 ? double.NaN : (double)s.triangles)) +
            ",\"limitations\":\"暂停规则和AI的短时固定镜头基线；不是对局CPU、热稳定、30分钟持续运行或Android性能验收。截图在预热前独立采集，只有棋盘相机画面；未包含IMGUI。ProfilerRecorder和内存计数器存在观测开销。\"}";
        File.WriteAllText(Path.Combine(path, "summary.json"), json, new UTF8Encoding(false));
    }
    Device DescribeDevice()
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        return new Device
        {
            build = M6App.BuildVersion, unity = Application.unityVersion, os = SystemInfo.operatingSystem,
            cpu = SystemInfo.processorType, cpuThreads = SystemInfo.processorCount, gpu = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsDriver = SystemInfo.graphicsDeviceVersion,
            systemMemoryMb = SystemInfo.systemMemorySize, gpuMemoryMb = SystemInfo.graphicsMemorySize,
            width = Screen.width, height = Screen.height, quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount,
            msaa = urp != null ? urp.msaaSampleCount : QualitySettings.antiAliasing,
            renderScale = urp != null ? urp.renderScale : 1, shadowDistance = urp != null ? urp.shadowDistance : QualitySettings.shadowDistance,
            pipeline = urp != null ? urp.name : "unknown", developmentBuild = Debug.isDebugBuild,
            frameTimingFeatureEnabled = FrameTimingManager.IsFeatureEnabled(), runInBackground = Application.runInBackground,
            launchMode = (visibleWindow ? "Normal/visible" : "Hidden/background") +
                (captureOnly ? "；仅截图模式不要求持续渲染，不得作为帧率通过" : "；焦点和持续渲染另行实测")
        };
    }
    void WriteCaptureResults(string path, string scenario, Census census)
    {
        // 单次快照，无峰值、区间分配量或帧率含义；不启动 Recorder 或 FrameTiming 采样。
        long allocated = Profiler.GetTotalAllocatedMemoryLong(), reserved = Profiler.GetTotalReservedMemoryLong();
        long mono = Profiler.GetMonoUsedSizeLong(), workingSet = WorkingSet();
        string json = "{\"schema\":\"ultra-baseline-capture-v1\",\"status\":\"captured-only\",\"performanceMeasured\":false,\"scenario\":" + Quote(scenario) +
            ",\"theme\":\"真实桌游\",\"device\":" + JsonUtility.ToJson(DescribeDevice(), true) +
            ",\"screenshots\":4,\"screenshotRoute\":\"v1四端点：全桌斜视、全桌高角、0.55近景、近景向右移0.8半高\"" +
            ",\"authorityUnchanged\":true,\"warmupSeconds\":" + Num(warmupSeconds) +
            ",\"sceneCensus\":" + JsonUtility.ToJson(census) + ",\"sampleCount\":0,\"frameMs\":null,\"cpuMs\":null,\"gpuMs\":null" +
            ",\"memorySnapshot\":{\"allocatedBytes\":" + allocated + ",\"reservedBytes\":" + reserved + ",\"monoUsedBytes\":" + mono +
            ",\"processWorkingSetBytes\":" + Counter(workingSet) + "}" +
            ",\"limitations\":\"仅显式URP棋盘截图及预热后当前离屏棋盘加载状态的资源/内存单次快照；隐藏窗口可能不执行完整IMGUI，不能等同正常前台完整UI常驻内存。没有逐帧测量、内存峰值、连续渲染或帧率通过，不含Android真机验收。\"}";
        File.WriteAllText(Path.Combine(path, "summary.json"), json, new UTF8Encoding(false));
    }
    static string Distribution(System.Collections.Generic.IEnumerable<double> source)
    {
        var values = source.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToArray();
        return "{\"validSamples\":" + values.Length + ",\"p50\":" + Num(Percentile(values, .5)) +
            ",\"p95\":" + Num(Percentile(values, .95)) + ",\"p99\":" + Num(Percentile(values, .99)) +
            ",\"max\":" + Num(values.Length == 0 ? double.NaN : values[values.Length - 1]) + "}";
    }
    static double Percentile(double[] sorted, double q) => sorted.Length == 0 ? double.NaN : sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * q) - 1)];
    static string Num(double value) => double.IsNaN(value) || double.IsInfinity(value) ? "null" : value.ToString("0.######", Invariant);
    static string CsvNumber(double value) => double.IsNaN(value) ? "" : Num(value);
    static string Counter(long value) => value < 0 ? "null" : value.ToString(Invariant);
    static string CsvCounter(long value) => value < 0 ? "" : value.ToString(Invariant);
    static string Bool(bool value) => value ? "true" : "false";
    static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    static double Clock() => (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
    static long WorkingSet() { try { using (var process = System.Diagnostics.Process.GetCurrentProcess()) return process.WorkingSet64; } catch { return -1; } }
    static ProfilerRecorder StartRecorder(ProfilerCategory category, string name) { try { return ProfilerRecorder.StartNew(category, name, 1); } catch { return default; } }
    // Valid 只说明句柄有效；发布构建中计数器可能尚未写入任何样本。
    // 无样本是明确的不可用状态，不能读取 LastValue，也不能伪报 0。
    static long Value(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? recorder.LastValue : -1;
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    void StopRecorders() { gcRecorder.Dispose(); drawRecorder.Dispose(); batchRecorder.Dispose(); triangleRecorder.Dispose(); }
    void OnDestroy() { RenderPipelineManager.endCameraRendering -= OnCameraRendered; StopRecorders(); }
    void Fail(Exception ex)
    {
        StopRecorders();
        Debug.LogError("CATAN_ULTRA_BASELINE_FAILED: " + ex.GetType().Name + "; stage=" + stage +
            "; operation=" + sampleOperation + "; sampledFrames=" + sampledFrames +
            "; renderedFrames=" + sampledRenderedFrames + "\n" + ex.StackTrace);
        if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"status\":\"failed\",\"errorType\":" + Quote(ex.GetType().Name) +
                ",\"stage\":" + Quote(stage) + ",\"operation\":" + Quote(sampleOperation) + ",\"sampledFrames\":" + sampledFrames +
                ",\"renderedFrames\":" + sampledRenderedFrames + ",\"hiddenWindow\":" + Bool(!visibleWindow) +
                ",\"stackTrace\":" + Quote(ex.StackTrace ?? "") + "}");
        Application.Quit(1);
    }
}
