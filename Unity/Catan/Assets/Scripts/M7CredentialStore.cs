using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// Windows 使用用户级 DPAPI；Android 使用系统 KeyStore 中不可导出的 AES/GCM 密钥。
// 任一平台不能安全保存时明确失败，绝不退回明文令牌。
internal static class M7CredentialStore
{
#if UNITY_EDITOR
    // macOS 编辑器仅用于菜单与传输验收；正式 Windows / Android 保管实现不变。
    // 合成凭据只存内存，磁盘占位不含凭据；不触碰普通玩家目录，也不冒充跨进程保管验证。
    static string verificationRoot;
    static readonly System.Collections.Generic.Dictionary<string,string> verificationValues=new System.Collections.Generic.Dictionary<string,string>();
    internal static void EnableEditorVerification(string directory)
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--online-lobby-verify")<0)
            throw new InvalidOperationException("仅显式在线大厅验收可使用编辑器凭据替身。");
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../..", ".local"))+Path.DirectorySeparatorChar;
        string requested=Path.GetFullPath(directory)+Path.DirectorySeparatorChar;
        if(!requested.StartsWith(root,StringComparison.Ordinal))throw new InvalidOperationException("验收凭据目录必须位于项目 .local。");
        verificationRoot=requested;verificationValues.Clear();
    }
    static bool IsEditorVerification(string path)=>verificationRoot!=null&&Path.GetFullPath(path).StartsWith(verificationRoot,StringComparison.Ordinal);
#endif
    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr value);
    static byte[] Transform(byte[] value,bool protect)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return TransformAndroid(value,protect);
#else
        if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new PlatformNotSupportedException("此平台尚无安全的联机凭据保管实现。");
        var input=new Blob{Length=value.Length,Data=Marshal.AllocHGlobal(value.Length)};Blob output=default;
        try
        {
            Marshal.Copy(value,0,input.Data,value.Length);
            bool ok=protect?CryptProtectData(ref input,"卡坦联机席位",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new IOException("无法安全保管席位凭据。");
            var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        }
        finally{for(int i=0;i<input.Length;i++)Marshal.WriteByte(input.Data,i,0);Marshal.FreeHGlobal(input.Data);if(output.Data!=IntPtr.Zero)LocalFree(output.Data);}
#endif
    }
#if UNITY_ANDROID && !UNITY_EDITOR
    const string AndroidAlias="Catan.M7.Credentials.v1";
    static readonly byte[] AndroidHeader=Encoding.ASCII.GetBytes("CATM7A01");
    const int AndroidIvBytes=12,AndroidTagBits=128;
    static AndroidJavaObject AndroidKey(bool create)
    {
        using(var keyStoreClass=new AndroidJavaClass("java.security.KeyStore"))
        using(var keyStore=keyStoreClass.CallStatic<AndroidJavaObject>("getInstance","AndroidKeyStore"))
        {
            // 固定接口签名，避免 Unity 根据 Android 内部密钥实现类推断重载。
            keyStore.Call(AndroidJNI.GetMethodID(keyStore.GetRawClass(),"load","(Ljava/security/KeyStore$LoadStoreParameter;)V"),new object[]{null});
            if(keyStore.Call<bool>("containsAlias",AndroidAlias))
                return keyStore.Call<AndroidJavaObject>(AndroidJNI.GetMethodID(keyStore.GetRawClass(),"getKey","(Ljava/lang/String;[C)Ljava/security/Key;"),AndroidAlias,null);
        }
        if(!create)throw new IOException("本设备的恢复身份密钥不存在，无法读取旧凭据。");
        using(var builder=new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder",AndroidAlias,3))
        using(var modes=builder.Call<AndroidJavaObject>("setBlockModes",new object[]{new[]{"GCM"}}))
        using(var padding=builder.Call<AndroidJavaObject>("setEncryptionPaddings",new object[]{new[]{"NoPadding"}}))
        using(var size=builder.Call<AndroidJavaObject>("setKeySize",256))
        using(var spec=builder.Call<AndroidJavaObject>("build"))
        using(var generatorClass=new AndroidJavaClass("javax.crypto.KeyGenerator"))
        using(var generator=generatorClass.CallStatic<AndroidJavaObject>("getInstance","AES","AndroidKeyStore"))
        {
            generator.Call(AndroidJNI.GetMethodID(generator.GetRawClass(),"init","(Ljava/security/spec/AlgorithmParameterSpec;)V"),spec);
            return generator.Call<AndroidJavaObject>("generateKey");
        }
    }
    static sbyte[] Signed(byte[] value)
    {
        var bytes=new sbyte[value.Length];Buffer.BlockCopy(value,0,bytes,0,value.Length);return bytes;
    }
    static byte[] Unsigned(sbyte[] value)
    {
        var bytes=new byte[value.Length];Buffer.BlockCopy(value,0,bytes,0,value.Length);return bytes;
    }
    static byte[] TransformAndroid(byte[] value,bool protect)
    {
        try
        {
            using(var key=AndroidKey(protect))
            using(var cipherClass=new AndroidJavaClass("javax.crypto.Cipher"))
            using(var cipher=cipherClass.CallStatic<AndroidJavaObject>("getInstance","AES/GCM/NoPadding"))
            {
                byte[] input=value,iv=null;
                if(protect)
                {
                    // 让 AndroidKeyStore 自行生成随机 IV，禁止调用方重用 nonce。
                    cipher.Call(AndroidJNI.GetMethodID(cipher.GetRawClass(),"init","(ILjava/security/Key;)V"),1,key);
                    iv=Unsigned(cipher.Call<sbyte[]>("getIV"));
                    if(iv.Length!=AndroidIvBytes)throw new IOException("系统返回的加密随机数格式不兼容。");
                }
                else
                {
                    if(value.Length<AndroidHeader.Length+AndroidIvBytes+AndroidTagBits/8)throw new IOException("恢复身份文件不完整。");
                    for(int i=0;i<AndroidHeader.Length;i++)if(value[i]!=AndroidHeader[i])throw new IOException("恢复身份文件版本不兼容。");
                    iv=new byte[AndroidIvBytes];Buffer.BlockCopy(value,AndroidHeader.Length,iv,0,iv.Length);
                    input=new byte[value.Length-AndroidHeader.Length-iv.Length];Buffer.BlockCopy(value,AndroidHeader.Length+iv.Length,input,0,input.Length);
                    using(var parameters=new AndroidJavaObject("javax.crypto.spec.GCMParameterSpec",AndroidTagBits,Signed(iv)))
                        cipher.Call(AndroidJNI.GetMethodID(cipher.GetRawClass(),"init","(ILjava/security/Key;Ljava/security/spec/AlgorithmParameterSpec;)V"),2,key,parameters);
                }
                cipher.Call("updateAAD",new object[]{Signed(AndroidHeader)});
                var signedInput=Signed(input);sbyte[] transformed=null;
                try
                {
                    transformed=cipher.Call<sbyte[]>("doFinal",new object[]{signedInput});
                    var result=Unsigned(transformed);
                    if(!protect)return result;
                    var envelope=new byte[AndroidHeader.Length+iv.Length+result.Length];
                    Buffer.BlockCopy(AndroidHeader,0,envelope,0,AndroidHeader.Length);
                    Buffer.BlockCopy(iv,0,envelope,AndroidHeader.Length,iv.Length);
                    Buffer.BlockCopy(result,0,envelope,AndroidHeader.Length+iv.Length,result.Length);return envelope;
                }
                finally{Array.Clear(signedInput,0,signedInput.Length);if(transformed!=null)Array.Clear(transformed,0,transformed.Length);}
            }
        }
        catch(AndroidJavaException error){throw new IOException("Android 系统密钥不可用，或恢复身份文件校验失败。",error);}
    }
#endif
    internal static void Write(string path,string value)
    {
#if UNITY_EDITOR
        if(IsEditorVerification(path))
        {verificationValues[Path.GetFullPath(path)]=value;File.WriteAllText(path,"仅编辑器验收占位；凭据保存在本进程内存。");return;}
#endif
        var plain=Encoding.UTF8.GetBytes(value);byte[] encrypted;
        try{encrypted=Transform(plain,true);}finally{Array.Clear(plain,0,plain.Length);}
        string temporary=path+".tmp";File.WriteAllBytes(temporary,encrypted);
#if UNITY_ANDROID && !UNITY_EDITOR
        // 同目录 rename 原子替换旧文件，避免 Android Mono 的 File.Replace 平台差异。
        try{using(var os=new AndroidJavaClass("android.system.Os"))os.CallStatic("rename",temporary,path);}
        catch(AndroidJavaException error){throw new IOException("无法原子保存本设备的恢复身份。",error);}
#else
        if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
#endif
    }
    internal static string Read(string path)
    {
#if UNITY_EDITOR
        if(IsEditorVerification(path))return verificationValues[Path.GetFullPath(path)];
#endif
        var plain=Transform(File.ReadAllBytes(path),false);
        try{return Encoding.UTF8.GetString(plain);}finally{Array.Clear(plain,0,plain.Length);}
    }

    // 仅显式开发参数触发；write / read 可跨进程验证密钥与密文持久化，无真实凭据入日志。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void VerifyWhenRequested()
    {
        string mode=null;var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"--m7-credential-verify");
        if(flag>=0&&flag+1<args.Length)mode=args[flag+1];
#if UNITY_ANDROID && !UNITY_EDITOR
        if(mode==null)
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
            {
                mode=intent.Call<string>("getStringExtra","catan_m7_credential_verify");
                if(mode!=null)intent.Call("removeExtra","catan_m7_credential_verify");
            }
#endif
        if(string.IsNullOrEmpty(mode))return;
        try
        {
            if(mode!="write"&&mode!="read"&&mode!="roundtrip")throw new InvalidOperationException("未知的凭据验收模式。");
            string directory=Path.Combine(Application.persistentDataPath,"m7-credential-verification");Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,"synthetic.credential"),value="仅用于验收的合成凭据-v1-7f329a";
            if(mode!="read")
            {
                Write(path,value);var first=File.ReadAllBytes(path);Write(path,value);var second=File.ReadAllBytes(path);
                if(Convert.ToBase64String(first)==Convert.ToBase64String(second))throw new IOException("重复保存没有产生独立随机密文。");
                if(Encoding.UTF8.GetString(second).Contains(value))throw new IOException("合成凭据被明文保存。");
                var tampered=(byte[])second.Clone();tampered[tampered.Length-1]^=1;
                string corrupt=path+".tampered";File.WriteAllBytes(corrupt,tampered);bool rejected=false;
                try{Read(corrupt);}catch(IOException){rejected=true;}finally{File.Delete(corrupt);}
                if(!rejected)throw new IOException("修改过的密文未被拒绝。");
            }
            if(Read(path)!=value)throw new IOException("恢复身份无法往返读取。");
            File.WriteAllText(Path.Combine(directory,"result.json"),"{\"status\":\"passed\",\"mode\":\""+mode+"\",\"platform\":\""+Application.platform+"\"}");
            Debug.Log("CATAN_M7_CREDENTIAL_STORE_OK: mode="+mode+"; platform="+Application.platform);Application.Quit(0);
        }
        catch(Exception error){Debug.LogError("CATAN_M7_CREDENTIAL_STORE_FAILED: type="+error.GetType().Name);Application.Quit(1);}
    }
}
