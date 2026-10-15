using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Windows 用户级 DPAPI。不能安全保存时明确失败，绝不退回明文令牌。
internal static class M7CredentialStore
{
    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr value);
    static byte[] Transform(byte[] value,bool protect)
    {
        if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new PlatformNotSupportedException("此联机验证版凭据保管仅支持 Windows。");
        var input=new Blob{Length=value.Length,Data=Marshal.AllocHGlobal(value.Length)};Blob output=default;
        try
        {
            Marshal.Copy(value,0,input.Data,value.Length);
            bool ok=protect?CryptProtectData(ref input,"卡坦联机席位",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new IOException("无法安全保管席位凭据。");
            var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        }
        finally{for(int i=0;i<input.Length;i++)Marshal.WriteByte(input.Data,i,0);Marshal.FreeHGlobal(input.Data);if(output.Data!=IntPtr.Zero)LocalFree(output.Data);}
    }
    internal static void Write(string path,string value)
    {
        var encrypted=Transform(Encoding.UTF8.GetBytes(value),true);string temporary=path+".tmp";File.WriteAllBytes(temporary,encrypted);
        if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
    }
    internal static string Read(string path)=>Encoding.UTF8.GetString(Transform(File.ReadAllBytes(path),false));
}
