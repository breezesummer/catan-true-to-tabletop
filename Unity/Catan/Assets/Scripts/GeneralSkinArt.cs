using System;
using System.Collections.Generic;
using UnityEngine;

internal static class GeneralSkinArt
{
    const string Root="GeneralUI/Dynamic/";
    const string FirstSkin="tk-g01";
    const string LegacyRoot="GeneralUI/ZhangLiao/";
    const string Preference="catan.general.static.";
    const int TextureLimit=24;
    [Serializable] internal sealed class Skin
    {
        public string id,sound;
        public float idleTime,attackTime;
        public int idleFrames;
        public bool slash;
        [NonSerialized] public bool useStatic;
        [NonSerialized] public string idlePath,attackPath;
    }
    [Serializable] sealed class Manifest { public Skin[] entries; public float slashTime; }
    sealed class TextureSlot { public ResourceRequest request; public int lastFrame; }
    static readonly Dictionary<string,TextureSlot> textures=new Dictionary<string,TextureSlot>();
    static readonly Dictionary<string,Skin> skins=new Dictionary<string,Skin>();
    static readonly Dictionary<string,ResourceRequest> sounds=new Dictionary<string,ResourceRequest>();
    internal static ResourceRequest AttackSound(Skin skin)
    {
        string key=skin?.sound??"shared/attack-sound";
        if(!sounds.TryGetValue(key,out var request))
            sounds[key]=request=Resources.LoadAsync<AudioClip>(Root+key);
        return request;
    }
    static Manifest manifest;
    internal static Skin Get(string id)
    {
        if(string.IsNullOrEmpty(id))return null;
        if(manifest==null)
        {
            manifest=JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>(Root+"manifest").text);
            foreach(var skin in manifest.entries)
            {
                skin.idlePath=skin.id==FirstSkin?LegacyRoot+"idle":Root+skin.id+"/idle";
                skin.attackPath=skin.id==FirstSkin?LegacyRoot+"raid":Root+skin.id+"/attack";
                skin.useStatic=PlayerPrefs.GetInt(Preference+skin.id,0)!=0;
                skins.Add(skin.id,skin);
            }
        }
        return skins.TryGetValue(id,out var found)?found:null;
    }
    internal static void SetStatic(Skin skin,bool value)
    {
        if(skin.useStatic==value)return;
        skin.useStatic=value;PlayerPrefs.SetInt(Preference+skin.id,value?1:0);PlayerPrefs.Save();
    }
    static Texture2D Texture(string path)
    {
        if(!textures.TryGetValue(path,out var slot))
        {
            if(textures.Count==TextureLimit)
            {
                string oldest=null;int last=int.MaxValue;
                foreach(var pair in textures)
                    if(pair.Value.request.isDone&&pair.Value.lastFrame<Time.frameCount-1&&pair.Value.lastFrame<last)
                    {oldest=pair.Key;last=pair.Value.lastFrame;}
                if(oldest==null)return null;
                var asset=textures[oldest].request.asset;
                textures.Remove(oldest);
                if(asset!=null)Resources.UnloadAsset(asset);
            }
            slot=new TextureSlot{request=Resources.LoadAsync<Texture2D>(path)};
            textures.Add(path,slot);
        }
        slot.lastFrame=Time.frameCount;
        return slot.request.isDone?slot.request.asset as Texture2D:null;
    }
    internal static bool IdleReady(Skin skin)
    {
        if(skin==null||skin.useStatic)return false;
        bool ready=Texture(skin.idlePath)!=null;
        if(skin.id==FirstSkin)
        {
            ready&=Texture(LegacyRoot+"scene")!=null;
            ready&=Texture(LegacyRoot+"wei-arc")!=null;
        }
        return ready;
    }
    internal static bool AttackReady(Skin skin)
    {
        var sound=AttackSound(skin);
        bool ready=skin==null||Texture(skin.attackPath)!=null;
        if(skin==null||skin.slash)ready&=Texture(Root+"shared/attack")!=null;
        return ready&&sound.isDone;
    }
    internal static float AttackSeconds(Skin skin)=>skin?.attackTime??manifest.slashTime;
    internal static void Idle(Rect area,Skin skin)
    {
        if(Event.current.type!=EventType.Repaint)return;
        bool first=skin.id==FirstSkin;
        if(first)GUI.DrawTexture(area,Texture(LegacyRoot+"scene"));
        float phase=(float)((Time.unscaledTimeAsDouble%skin.idleTime)/skin.idleTime);
        Sheet(area,Texture(skin.idlePath),phase,skin.idleFrames,first?8:16);
        if(first)GUI.DrawTexture(new Rect(area.x,area.y,area.width,area.height*73/256),Texture(LegacyRoot+"wei-arc"));
    }
    internal static Rect CenterArea=>new Rect(720-315,450-262.5f,630,525);
    internal static void Attack(Skin skin,float phase)
    {
        var area=CenterArea;
        if(skin!=null)
            Sheet(area,Texture(skin.attackPath),phase,skin.id==FirstSkin?66:48,8);
        if(skin==null||skin.slash)
            Sheet(area,Texture(Root+"shared/attack"),phase,48,8);
    }
    static void Sheet(Rect area,Texture2D texture,float phase,int count,int columns)
    {
        if(texture==null)return;
        int index=Mathf.Clamp((int)(phase*count),0,count-1);
        int rows=(count+columns-1)/columns;
        float x=.5f/texture.width,y=.5f/texture.height;
        var uv=new Rect(index%columns/(float)columns+x,1-(index/columns+1)/(float)rows+y,
            1f/columns-2*x,1f/rows-2*y);
        GUI.DrawTextureWithTexCoords(area,texture,uv,true);
    }
}
