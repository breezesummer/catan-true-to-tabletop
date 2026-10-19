using System.Net;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace Catan.Server;

public sealed class PrivateServerFiles : IDisposable
{
    // Schannel 握手要求持久化用户密钥；macOS 不支持 EphemeralKeySet，Linux 使用临时密钥。
    private static X509KeyStorageFlags KeyStorage => OperatingSystem.IsWindows()
        ? X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet
        : OperatingSystem.IsMacOS() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;
    private readonly FileStream instanceLock;
    public X509Certificate2? Certificate { get; }
    public string Fingerprint { get; } = "";

    public PrivateServerFiles(ServerOptions options)
    {
        MakePrivateDirectory(options.DataDirectory);
        try { instanceLock = new FileStream(Path.Combine(options.DataDirectory, "server.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
        { throw new ServerStartupException("DATA_IN_USE", "服务存档目录正由另一个进程使用，请关闭旧版游戏或重复服务。"); }
        try
        {
            if (!options.Online)
            {
                Certificate = LoadCertificate(options);
                Fingerprint = Certificate.GetCertHashString(HashAlgorithmName.SHA256);
                WritePrivate(Path.Combine(options.DataDirectory, "server-fingerprint.txt"), Fingerprint + Environment.NewLine);
            }
        }
        catch { instanceLock.Dispose(); throw; }
    }

    public static void MakePrivateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        if (OperatingSystem.IsWindows()) RestrictWindowsDirectory(path);
        else File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictWindowsDirectory(string path)
    {
        var identity = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("无法确定私有目录所有者。");
        var permissions = new DirectorySecurity();
        permissions.SetAccessRuleProtection(true, false);
        permissions.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(permissions);
    }

    public static void WritePrivate(string path, string content)
    {
        File.WriteAllText(path, content);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static X509Certificate2 LoadCertificate(ServerOptions options)
    {
        if (options.CertificatePath != null)
        {
            var provided = X509CertificateLoader.LoadPkcs12FromFile(options.CertificatePath, Environment.GetEnvironmentVariable("CATAN_CERTIFICATE_PASSWORD"), KeyStorage);
            if (!provided.HasPrivateKey || provided.NotAfter.ToUniversalTime() <= DateTime.UtcNow || provided.NotBefore.ToUniversalTime() > DateTime.UtcNow)
            {
                provided.Dispose();
                throw new InvalidOperationException("提供的服务证书无私钥、尚未生效或已过期。");
            }
            return provided;
        }
        var pfx = Path.Combine(options.DataDirectory, "lan-server.pfx");
        var secret = Path.Combine(options.DataDirectory, "lan-certificate.key");
        if (File.Exists(pfx))
        {
            if (!File.Exists(secret)) throw new InvalidOperationException("局域网证书密钥文件丢失；请恢复私有服务目录备份。");
            var existing = X509CertificateLoader.LoadPkcs12FromFile(pfx, File.ReadAllText(secret), KeyStorage);
            if (existing.NotAfter.ToUniversalTime() <= DateTime.UtcNow) { existing.Dispose(); throw new InvalidOperationException("局域网证书已过期，需显式重新配对后更换证书。"); }
            return existing;
        }
        using var key = RSA.Create(3072);
        var request = new CertificateRequest("CN=Catan M7 LAN", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        names.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        WritePrivate(secret, password);
        File.WriteAllBytes(pfx, generated.Export(X509ContentType.Pfx, password));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(pfx, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return X509CertificateLoader.LoadPkcs12FromFile(pfx, password, KeyStorage);
    }

    public void Dispose() { Certificate?.Dispose(); instanceLock.Dispose(); }
}
