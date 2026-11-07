using UnityEngine;
using UnityEngine.Rendering;

// 独立于落点预览的公开船选择框；不创建碰撞体，不参与移动是否合法的判定。
public sealed class TabletopShipSelection : MonoBehaviour
{
    Material material;
    public static TabletopShipSelection Create(Transform parent)
    {
        var root=new GameObject("所选船航线高亮 v010");root.transform.SetParent(parent,false);
        var highlight=root.AddComponent<TabletopShipSelection>();
        highlight.material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        highlight.material.color=new Color(.20f,.98f,.82f);
        var line=root.AddComponent<LineRenderer>();line.sharedMaterial=highlight.material;
        line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.0008f;line.positionCount=4;
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        line.SetPositions(new[]{new Vector3(-.0045f,.002f,-.0075f),new Vector3(.0045f,.002f,-.0075f),
            new Vector3(.0045f,.002f,.0075f),new Vector3(-.0045f,.002f,.0075f)});
        return highlight;
    }
    public void Show(Vector3 edgePosition,Quaternion edgeRotation)
    {
        transform.SetPositionAndRotation(edgePosition,edgeRotation);gameObject.SetActive(true);
    }
    void OnDestroy(){if(material!=null)Destroy(material);}
}
