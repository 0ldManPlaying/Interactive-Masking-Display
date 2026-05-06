using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace InteractiveMask.Display;

/// <summary>
/// Generates a self-signed PFX for Kestrel HTTPS in <c>%PROGRAMDATA%\InteractiveMask</c>.
/// The PFX is bound to the local machine name and the host's first non-loopback
/// IPv4 address as Subject Alternative Names so browsers don't refuse it for
/// hostname-mismatch on a typical LAN deployment.
/// </summary>
public static class CertificateHelper
{
    public static (string Path, string Password) GenerateSelfSigned(int validYears = 5)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "InteractiveMask");
        Directory.CreateDirectory(dir);
        var pfxPath = Path.Combine(dir, "webhost.pfx");

        using var rsa = RSA.Create(2048);
        var hostname = Dns.GetHostName();
        var req = new CertificateRequest(
            $"CN={hostname}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        req.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // serverAuth
                true));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(hostname);
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        foreach (var ip in GetLocalIPv4Addresses())
        {
            sanBuilder.AddIpAddress(ip);
        }
        req.CertificateExtensions.Add(sanBuilder.Build());

        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(validYears));

        var password = GeneratePassword();
        File.WriteAllBytes(pfxPath, cert.Export(X509ContentType.Pfx, password));
        return (pfxPath, password);
    }

    private static IEnumerable<IPAddress> GetLocalIPv4Addresses()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(a));
        }
        catch
        {
            return Array.Empty<IPAddress>();
        }
    }

    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes);
    }
}
