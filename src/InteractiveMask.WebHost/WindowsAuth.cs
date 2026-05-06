using System.ComponentModel;
using System.Runtime.InteropServices;

namespace InteractiveMask.WebHost;

/// <summary>
/// Minimal LogonUser wrapper for AD-mode browser auth. Mirrors the helper of
/// the same name in InteractiveMask.Display so the WebHost can validate
/// Windows credentials locally without taking a project reference on Display.
/// </summary>
internal static class WindowsAuth
{
    public sealed record Result(bool Success, string? Username, string? Error);

    private const int LOGON32_LOGON_NETWORK = 3;
    private const int LOGON32_PROVIDER_DEFAULT = 0;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(
        string lpszUsername,
        string? lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out IntPtr phToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    public static Result Authenticate(string username, string password, string? domain)
    {
        if (string.IsNullOrEmpty(username)) return new Result(false, null, "username missing");
        if (password is null) return new Result(false, null, "password missing");

        string user = username;
        string? dom = string.IsNullOrWhiteSpace(domain) ? null : domain;

        int slash = username.IndexOf('\\');
        if (slash > 0)
        {
            dom = username[..slash];
            user = username[(slash + 1)..];
        }
        else
        {
            int at = username.IndexOf('@');
            if (at > 0)
            {
                user = username[..at];
                dom = username[(at + 1)..];
            }
        }

        IntPtr token = IntPtr.Zero;
        try
        {
            bool ok = LogonUser(user, dom, password,
                LOGON32_LOGON_NETWORK, LOGON32_PROVIDER_DEFAULT, out token);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                return new Result(false, null, new Win32Exception(err).Message);
            }
            var canonical = string.IsNullOrEmpty(dom) ? user : $"{dom}\\{user}";
            return new Result(true, canonical, null);
        }
        finally
        {
            if (token != IntPtr.Zero) CloseHandle(token);
        }
    }
}
