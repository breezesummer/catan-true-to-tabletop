using UnityEngine;

// 落点跟随合法目标切换，进入后只播放一次。
internal sealed class SelectionArrival : MonoBehaviour
{
    Vector3 fullScale;
    float started;
    void Awake()
    {
        fullScale=transform.localScale;
        started=Time.unscaledTime;
        transform.localScale=fullScale*.86f;
    }
    void Update()
    {
        float t=Mathf.Clamp01((Time.unscaledTime-started)/.16f);
        transform.localScale=fullScale*Mathf.SmoothStep(.86f,1,t);
        if(t>=1)enabled=false;
    }
}
