using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace InteractiveMask.Display;

/// <summary>
/// Optional kiosk hardening: while active, a low-level keyboard hook swallows
/// the most common escape routes (Win-key, Alt+Tab, Alt+F4, Ctrl+Esc, Alt+Esc)
/// and the bound window is forced topmost so the taskbar can't peek through.
///
/// Out of scope: Ctrl+Alt+Del. The Secure Attention Sequence is handled by
/// winlogon at higher privilege than user-mode hooks; blocking it requires a
/// shell-replacement / GPO policy on the host machine, not application code.
/// </summary>
public sealed class KioskGuard : IDisposable
{
    public bool IsActive { get; private set; }

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc; // keep alive: native code holds a function pointer to it
    private Window? _boundWindow;

    public void Activate(Window window)
    {
        if (IsActive) return;
        _boundWindow = window;
        _proc = HookCallback;
        _hookId = InstallHook(_proc);
        window.Topmost = true;
        IsActive = true;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _proc = null;
        if (_boundWindow is not null) _boundWindow.Topmost = false;
        _boundWindow = null;
        IsActive = false;
    }

    public void Dispose() => Deactivate();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (ShouldSuppress(data.vkCode))
            {
                return new IntPtr(1); // swallow the key
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool ShouldSuppress(uint vk)
    {
        // Windows logo keys ⇒ block both halves of Win+L, Win+E, Win+R, etc.
        if (vk == VK_LWIN || vk == VK_RWIN) return true;

        bool altDown  = IsDown(VK_MENU);
        bool ctrlDown = IsDown(VK_CONTROL);

        // Alt+F4 — kiosk exit must go through the right-click menu + admin PIN.
        if (vk == VK_F4 && altDown) return true;
        // Alt+Tab — task switcher.
        if (vk == VK_TAB && altDown) return true;
        // Alt+Esc — cycle through windows.
        if (vk == VK_ESCAPE && altDown) return true;
        // Ctrl+Esc — Start menu.
        if (vk == VK_ESCAPE && ctrlDown) return true;

        return false;
    }

    private static IntPtr InstallHook(LowLevelKeyboardProc proc)
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    // ---- P/Invoke ----------------------------------------------------------

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN     = 0x0100;
    private const int WM_SYSKEYDOWN  = 0x0104;
    private const int VK_LWIN        = 0x5B;
    private const int VK_RWIN        = 0x5C;
    private const int VK_TAB         = 0x09;
    private const int VK_ESCAPE      = 0x1B;
    private const int VK_F4          = 0x73;
    private const int VK_MENU        = 0x12; // Alt
    private const int VK_CONTROL     = 0x11;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
