using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class FishingF6SampleVerification
{
    public static void ReimportAndVerify()
    {
        foreach(var path in Directory.GetFiles("Assets/Resources/HarborFishing/v001","*.fbx"))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        VerifyAll();
    }
    public static void VerifyAll()
    {
        foreach(var name in new[]{"fish-1","fish-2","fish-3","fish-back","old-boot","harbormaster","ground-4","ground-5","ground-6","ground-8","ground-9","ground-10","lake"})
        {
            var model=Resources.Load<GameObject>(FishingTabletopAssets.Resource(name));var icon=Resources.Load<Texture2D>(FishingTabletopAssets.Resource(name));
            if(model==null||icon==null)throw new Exception("组件或图标缺失："+name);
            var instance=EntityBoardAssets.ReferenceMarker(FishingTabletopAssets.Resource(name),name,Vector3.zero,Quaternion.identity,null);var renderers=instance.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            if(Math.Abs(bounds.min.y)>.0005f||bounds.max.y>.016f)throw new Exception("组件高度／底面错误："+name);
            if(name=="lake"&&(Math.Abs(bounds.size.x-.0684f)>.001f||Math.Abs(bounds.size.z-.0788f)>.001f))throw new Exception("湖泊未匹配尖顶六边形");
            if(instance.GetComponentsInChildren<Collider>().Length!=0||renderers.Length!=1)throw new Exception("组件碰撞体或网格数错误："+name);
            if(renderers.Any(r=>r.sharedMaterials.Any(m=>m.shader.name!="Universal Render Pipeline/Lit")))throw new Exception("组件材质错误："+name);
            UnityEngine.Object.DestroyImmediate(instance);
        }
        Debug.Log("F6全部13件模型、图标、枢轴、URP及无碰撞体检查通过");
    }
    public static void Run()
    {
        foreach(var name in new[]{"fish-1","ground-4"})
        {
            var model=Resources.Load<GameObject>(FishingTabletopAssets.Resource(name));
            if(model==null)throw new Exception("模型缺失："+name);
            var instance=EntityBoardAssets.ReferenceMarker(FishingTabletopAssets.Resource(name),name,Vector3.zero,Quaternion.identity,null);
            var renderers=instance.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float width=name=="fish-1"?.024f:.042f;
            if(Math.Abs(bounds.size.x-width)>.001f||Math.Abs(bounds.min.y)>.0005f||bounds.max.y>.004f)throw new Exception("模型尺度／底面错误："+name+" "+bounds);
            if(instance.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("模型包含碰撞体");
            if(renderers.Any(r=>r.sharedMaterials.Any(m=>m.shader.name!="Universal Render Pipeline/Lit")))throw new Exception("材质未适配URP");
            Debug.Log("F6样板通过 "+name+" "+bounds);UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
