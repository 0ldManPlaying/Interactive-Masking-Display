using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace InteractiveMask.Display;

/// <summary>
/// Stores and verifies the admin PIN that gates kiosk-exit and setup. The PIN
/// is encrypted with Windows DPAPI (LocalMachine scope) so the file in
/// ProgramData is unreadable on any other machine. Consequence: the binary
/// must run on the same machine where the PIN was set; admins moving disks
/// must reset the PIN.
/// </summary>
public sealed class AdminPinService
{
    private readonly string _path;

    public AdminPinService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "InteractiveMask");
        try { Directory.CreateDirectory(dir); } catch { }
        _path = Path.Combine(dir, "admin.dat");
    }

    public bool IsConfigured => File.Exists(_path);

    /// <summary>Set or replace the admin PIN. The encrypted blob is rewritten atomically.</summary>
    public void SetPin(string pin)
    {
        if (string.IsNullOrEmpty(pin)) throw new ArgumentException("pin must not be empty", nameof(pin));
        var encrypted = ProtectedData.Protect(
            userData: Encoding.UTF8.GetBytes(pin),
            optionalEntropy: null,
            scope: DataProtectionScope.LocalMachine);

        var temp = _path + ".tmp";
        File.WriteAllBytes(temp, encrypted);
        File.Move(temp, _path, overwrite: true);
    }

    public bool Verify(string pin)
    {
        if (!IsConfigured) return false;
        try
        {
            var encrypted = File.ReadAllBytes(_path);
            var stored = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(stored),
                Encoding.UTF8.GetBytes(pin));
        }
        catch
        {
            // Corrupted or unreadable file ⇒ refuse access. Admin can wipe the
            // file from disk and the next launch forces a fresh PIN setup.
            return false;
        }
    }
}
