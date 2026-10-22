#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

// 安装包元数据也限定双向横屏，避免 Android 在 Unity 启动前短暂切成竖屏。
public sealed class AndroidLandscapeManifest : IPostGenerateGradleAndroidProject
{
    public int callbackOrder { get { return 100; } }

    public void OnPostGenerateGradleAndroidProject(string basePath)
    {
        string path = Path.Combine(basePath, "src/main/AndroidManifest.xml");
        var document = new XmlDocument();
        document.Load(path);
        const string android = "http://schemas.android.com/apk/res/android";
        foreach (XmlElement activity in document.SelectNodes("/manifest/application/activity"))
        {
            string name = activity.GetAttribute("name", android);
            if (name != "com.unity3d.player.UnityPlayerActivity") continue;
            activity.SetAttribute("screenOrientation", android, "sensorLandscape");
            document.Save(path);
            Debug.Log("CATAN_ANDROID_LANDSCAPE_MANIFEST_OK");
            return;
        }
        throw new InvalidDataException("Android 主活动缺失，未能限定横屏。");
    }
}
#endif
