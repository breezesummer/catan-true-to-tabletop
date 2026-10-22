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
        public double elapsed, milliseconds, cpu, gpu, main, render, wait, gui, update;
        public ulong timingStamp;
        public long mainAllocated;
        public long allocated, reserved, mono, gcBytes, draws, batches, triangles;
        public int generation0, generation1, generation2, renderedFrames;
        public bool focused;
    }
    readonly FrameTiming[] timing = new FrameTiming[1];
    readonly string[] scenarios = { "ordinary", "maximum", "crowded", "fog" };
    M6App app;
    TabletopScene table;
    Camera camera;
    string output, fixtures, stage = "初始化";
    float sampleSeconds, warmupSeconds;
    int drainFrames;
    bool mainAllocationAvailable;
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
            drainFrames = int.Parse(Argument(args, "--ultra-baseline-drain", "16"), Invariant);
            Require(drainFrames >= 0 && drainFrames <= 120, "无效时序排空帧数。");
            FrameCost.Enabled = true; FrameCost.MainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            long allocationCheck = GC.GetAllocatedBytesForCurrentThread();
            var allocationSentinel = new byte[4096];
            mainAllocationAvailable = GC.GetAllocatedBytesForCurrentThread() - allocationCheck >= 4096;
            GC.KeepAlive(allocationSentinel);
            sampleSeconds = float.Parse(Argument(args, "--ultra-baseline-seconds", "30"), Invariant);
            warmupSeconds = float.Parse(Argument(args, "--ultra-baseline-warmup", "8"), Invariant);
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
        File.WriteAllText(Path.Combine(output, "result.json"), "{\"status\":\"passed\",\"scenarios\":4,\"authorityUnchanged\":true,\"screenshotsPerScenario\":4,\"androidDeviceVerified\":false}", new UTF8Encoding(false));
        Debug.Log("CATAN_ULTRA_BASELINE_OK: four fixtures; fixed camera route; raw frame samples; authority unchanged");
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
            var census = TakeCensus();
            stage = scenario + "：预热";
            double warmup = Clock();
            while (Clock() - warmup < warmupSeconds) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
            double prepareStart = Clock();
            Require(app.Paused, "预热阶段意外恢复 AI。");
            string before = app.ExportEnvelope();
            double exportMs = (Clock() - prepareStart) * 1000;
            double bufferStart = Clock();
            var samples = new Sample[Math.Min(1200000, Mathf.CeilToInt(sampleSeconds * 2000) + 10)];
            double bufferMs = (Clock() - bufferStart) * 1000;
            gcRecorder = StartRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            drawRecorder = StartRecorder(ProfilerCategory.Render, "Draw Calls Count");
            batchRecorder = StartRecorder(ProfilerCategory.Render, "Batches Count");
            triangleRecorder = StartRecorder(ProfilerCategory.Render, "Triangles Count");
            bool gcAvailable = gcRecorder.Valid, drawAvailable = drawRecorder.Valid;
            int count = 0, renderStart = renderedFrames;
            int[] gcStart = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            long processStart = WorkingSet();
            ulong lastTiming = 0;
            // 分配缓冲区和启动 Recorder 的时间不算入采样。
            yield return null;
            double preparationMs = (Clock() - prepareStart) * 1000;
            for (int drain = 0; drain < drainFrames; drain++) { FrameTimingManager.CaptureFrameTimings(); yield return null; }
            for (int i = 0; i < FrameCost.Ticks.Length; i++) System.Threading.Interlocked.Exchange(ref FrameCost.Ticks[i], 0);
            long previousAllocated = GC.GetAllocatedBytesForCurrentThread();
            double start = Clock(), previous = start;
            long sampleStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            stage = scenario + "：逐帧采样";
            FrameTimingManager.CaptureFrameTimings();
            while (true)
            {
                yield return null;
                double now = Clock();
                if (count >= samples.Length) throw new InvalidOperationException("采样容量不足，拒绝静默截断。 elapsed=" + (now-start) + "; rendered=" + (renderedFrames-renderStart));
                double cpu = double.NaN, gpu = double.NaN, main = double.NaN, render = double.NaN, wait = double.NaN;
                ulong stamp = 0;
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != lastTiming)
                {
                    lastTiming = timing[0].frameStartTimestamp;
                    stamp = lastTiming;
                    main = timing[0].cpuMainThreadFrameTime; render = timing[0].cpuRenderThreadFrameTime; wait = timing[0].cpuMainThreadPresentWaitTime;
                    if (timing[0].cpuFrameTime > 0) cpu = timing[0].cpuFrameTime;
                    if (timing[0].gpuFrameTime > 0) gpu = timing[0].gpuFrameTime;
                }
                long currentAllocated = GC.GetAllocatedBytesForCurrentThread();
                samples[count++] = new Sample
                {
                    elapsed = now - start, milliseconds = (now - previous) * 1000,
                    cpu = cpu, gpu = gpu, main = main, render = render, wait = wait, timingStamp = stamp,
                    mainAllocated = mainAllocationAvailable ? currentAllocated - previousAllocated : -1,
                    gui = TakeStage(FrameCost.Stage.Gui), update = TakeStage(FrameCost.Stage.GameUpdate), allocated = Profiler.GetTotalAllocatedMemoryLong(),
                    reserved = Profiler.GetTotalReservedMemoryLong(), mono = Profiler.GetMonoUsedSizeLong(),
                    gcBytes = Value(gcRecorder), draws = Value(drawRecorder), batches = Value(batchRecorder), triangles = Value(triangleRecorder),
                    generation0 = GC.CollectionCount(0), generation1 = GC.CollectionCount(1), generation2 = GC.CollectionCount(2),
                    focused = Application.isFocused, renderedFrames = renderedFrames - renderStart
                };
                previous = now; previousAllocated = currentAllocated;
                if (now - start >= sampleSeconds) break;
                ApplyRoute(poses, (float)((now - start) / sampleSeconds));
                FrameTimingManager.CaptureFrameTimings();
            }
            int[] gcEnd = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
            long processEnd = WorkingSet();
            StopRecorders();
            stage = scenario + "：状态与渲染检查";
            Require(app.Paused && before == app.ExportEnvelope(), "采样期间权威状态发生变化。");
            Require(renderedFrames - renderStart >= count * .9, "采样期间相机没有持续渲染；后台结果无效。");
            WriteResults(scenarioDirectory, scenario, samples, count, gcStart, gcEnd, processStart, processEnd, census, gcAvailable, drawAvailable);
            File.WriteAllText(Path.Combine(scenarioDirectory, "preparation.json"), "{\"preparationMs\":" + Num(preparationMs) + ",\"exportMs\":" + Num(exportMs) + ",\"bufferMs\":" + Num(bufferMs) + ",\"sampleStartStopwatchTicks\":" + sampleStartTicks + ",\"stopwatchFrequency\":" + System.Diagnostics.Stopwatch.Frequency + ",\"mainAllocationAvailable\":" + Bool(mainAllocationAvailable) + ",\"drainFrames\":" + drainFrames + ",\"cpuTimerFrequency\":" + FrameTimingManager.GetCpuTimerFrequency() + "}");
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
    void WriteResults(string path, string scenario, Sample[] buffer, int count, int[] gcStart, int[] gcEnd, long processStart, long processEnd, Census census, bool gcAvailable, bool drawsAvailable)
    {
        var samples = buffer.Take(count).ToArray();
        using (var csv = new StreamWriter(Path.Combine(path, "frames.csv"), false, new UTF8Encoding(false)))
        {
            csv.WriteLine("frame,elapsedSeconds,frameMs,cpuMs,gpuMs,allocatedBytes,reservedBytes,monoUsedBytes,gcAllocatedBytes,gc0,gc1,gc2,drawCalls,batches,triangles,focused,renderedFrames,cpuMainMs,cpuRenderMs,presentWaitMs,timingTimestamp,mainThreadAllocatedBytes,guiMs,gameUpdateMs");
            for (int i = 0; i < samples.Length; i++)
            {
                var s = samples[i];
                csv.WriteLine(i + "," + Num(s.elapsed) + "," + Num(s.milliseconds) + "," + CsvNumber(s.cpu) + "," + CsvNumber(s.gpu) + "," +
                    s.allocated + "," + s.reserved + "," + s.mono + "," + CsvCounter(s.gcBytes) + "," + s.generation0 + "," + s.generation1 + "," + s.generation2 + "," +
                    CsvCounter(s.draws) + "," + CsvCounter(s.batches) + "," + CsvCounter(s.triangles) + "," + (s.focused ? 1 : 0) + "," + s.renderedFrames + "," + CsvNumber(s.main) + "," + CsvNumber(s.render) + "," + CsvNumber(s.wait) + "," + s.timingStamp + "," + s.mainAllocated + "," + Num(s.gui) + "," + Num(s.update));
            }
        }
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var device = new Device
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
            launchMode = "窗口启动方式见launch.json；focusedFrames记录实际焦点；固定路线自动测量"
        };
        string json = "{\n\"schema\":\"ultra-baseline-performance-v2\",\"scenario\":" + Quote(scenario) +
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
            ",\"gcRecorderAvailable\":" + Bool(gcAvailable) + ",\"renderCountersAvailable\":" + Bool(drawsAvailable) +
            ",\"gcCollections\":[" + (gcEnd[0] - gcStart[0]) + "," + (gcEnd[1] - gcStart[1]) + "," + (gcEnd[2] - gcStart[2]) + "]" +
            ",\"gcAllocatedBytesTotal\":" + (gcAvailable ? samples.Sum(s => Math.Max(0, s.gcBytes)).ToString(Invariant) : "null") +
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
    static double TakeStage(FrameCost.Stage stage) => System.Threading.Interlocked.Exchange(ref FrameCost.Ticks[(int)stage], 0) * 1000d / System.Diagnostics.Stopwatch.Frequency;
    static double Clock() => (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
    static long WorkingSet() { try { using (var process = System.Diagnostics.Process.GetCurrentProcess()) return process.WorkingSet64; } catch { return -1; } }
    static ProfilerRecorder StartRecorder(ProfilerCategory category, string name) { try { return ProfilerRecorder.StartNew(category, name, 1); } catch { return default; } }
    static long Value(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : -1;
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    void StopRecorders() { gcRecorder.Dispose(); drawRecorder.Dispose(); batchRecorder.Dispose(); triangleRecorder.Dispose(); }
    void OnDestroy() { FrameCost.Enabled = false; RenderPipelineManager.endCameraRendering -= OnCameraRendered; StopRecorders(); }
    void Fail(Exception ex)
    {
        StopRecorders();
        Debug.LogError("CATAN_ULTRA_BASELINE_FAILED: " + ex.GetType().Name + "; stage=" + stage + "; " + ex.Message);
        if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"status\":\"failed\",\"errorType\":" + Quote(ex.GetType().Name) + ",\"stage\":" + Quote(stage) + "}");
        Application.Quit(1);
    }
}
