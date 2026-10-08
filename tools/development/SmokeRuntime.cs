using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Catan.EnvironmentProbe;

public class SmokeRuntime : MonoBehaviour
{
    private IEnumerator Start()
    {
        var arguments = Environment.GetCommandLineArgs();
        bool smoke = Array.IndexOf(arguments, "--environment-smoke") >= 0;
        int render = Array.IndexOf(arguments, "--environment-render");
        if (!smoke && render < 0) yield break;
        if (Probe.RoundTrip("卡坦岛环境验证") != "卡坦岛环境验证")
        {
            Debug.LogError("Environment probe failed in player.");
            Application.Quit(1);
            yield break;
        }
        if (render >= 0)
        {
            if (render + 1 >= arguments.Length || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError("Graphics validation requires a screenshot path and a graphics device.");
                Application.Quit(1);
                yield break;
            }
            string screenshot = Path.GetFullPath(arguments[render + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(screenshot));
            for (int frame = 0; frame < 8; frame++) yield return null;
            var target = new RenderTexture(800, 600, 24, RenderTextureFormat.ARGB32);
            target.Create();
            var camera = FindFirstObjectByType<Camera>();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request))
                throw new InvalidOperationException("URP render requests are unavailable.");
            RenderPipeline.SubmitRenderRequest(camera, request);
            var previousTarget = RenderTexture.active;
            RenderTexture.active = target;
            var capture = new Texture2D(800, 600, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
            capture.Apply();
            RenderTexture.active = previousTarget;
            target.Release();
            Destroy(target);
            int greenPixels = 0;
            foreach (var pixel in capture.GetPixels32())
                if (pixel.g > pixel.r + 10 && pixel.g > pixel.b + 10) greenPixels++;
            File.WriteAllBytes(screenshot, capture.EncodeToPNG());
            Destroy(capture);
            if (greenPixels < 100)
            {
                Debug.LogError("Graphics validation did not render the green test model.");
                Application.Quit(1);
                yield break;
            }
            Debug.Log("CATAN_ENVIRONMENT_RENDER_OK: " + SystemInfo.graphicsDeviceName + "; green pixels=" + greenPixels);
        }
        else Debug.Log("CATAN_ENVIRONMENT_PLAYER_OK");
        Application.Quit(0);
    }
}
