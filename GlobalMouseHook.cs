using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SwiftScroll;

public sealed class MouseWheelEventArgs : EventArgs
{
    public int Delta { get; }
    public bool Handled { get; set; }
    public int MouseX { get; }
    public int MouseY { get; }
    public MouseWheelEventArgs(int delta, int x = 0, int y = 0)
    {
        Delta = delta;
        MouseX = x;
        MouseY = y;
    }
}

// Low-level mouse hook with ability to mark wheel events handled
public sealed class GlobalMouseHook : IDisposable
{
    private IntPtr _hook = IntPtr.Zero;
    private HookProc? _proc;
    
    // Performance optimization: cache keyboard state to avoid repeated API calls
    private long _lastKeyStateCheckTick;
    private bool _cachedShiftState;
    private const int KEY_STATE_CACHE_MS = 50; // Cache for 50ms

    // Performance optimization: cache taskbar window handles
    private static long _lastTaskbarCheckTick;
    private static IntPtr _cachedPrimaryTaskbar = IntPtr.Zero;
    private static IntPtr _cachedSecondaryTaskbar = IntPtr.Zero;
    private const int TASKBAR_CACHE_MS = 2000; // Cache taskbar handles for 2 seconds

    public bool IsInstalled => _hook != IntPtr.Zero;

    /// <summary>
    /// When true, holding Shift will convert vertical scroll to horizontal.
    /// </summary>
    public bool ShiftKeyHorizontal { get; set; } = true;

    public event EventHandler<MouseWheelEventArgs>? MouseWheel;
    public event EventHandler<MouseWheelEventArgs>? MouseHWheel;

    public void Install()
    {
        if (IsInstalled) return;
        try
        {
            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            
            if (curModule == null)
            {
                Debug.WriteLine("[GlobalMouseHook] Failed to get main module");
                return;
            }
            
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            
            if (_hook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[GlobalMouseHook] Failed to install hook. Error code: {error}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMouseHook] Install error: {ex.Message}");
            _hook = IntPtr.Zero;
            _proc = null;
        }
    }

    public void Uninstall()
    {
        if (!IsInstalled) return;
        try
        {
            if (!UnhookWindowsHookEx(_hook))
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[GlobalMouseHook] UnhookWindowsHookEx failed. Error code: {error}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMouseHook] Uninstall error: {ex.Message}");
        }
        finally
        {
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        try
        {
            int msg = wParam.ToInt32();
            
            // Attempt to marshal the structure safely
            if (!TryGetHookData(lParam, out var data))
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            // Ignore injected events to avoid feedback loops
            const int LLMHF_INJECTED = 0x00000001;
            const int LLMHF_LOWER_IL_INJECTED = 0x00000002;
            if ((data.flags & (LLMHF_INJECTED | LLMHF_LOWER_IL_INJECTED)) != 0)
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            // Check if mouse is over taskbar - if so, don't intercept
            if (IsMouseOverTaskbar(data.pt.x, data.pt.y))
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            if (msg == WM_MOUSEWHEEL)
            {
                int delta = (short)((data.mouseData >> 16) & 0xffff);
                var args = new MouseWheelEventArgs(delta, data.pt.x, data.pt.y);

                // If Shift is held and ShiftKeyHorizontal is enabled, route to horizontal
                if (ShiftKeyHorizontal && IsShiftPressedCached())
                {
                    MouseHWheel?.Invoke(this, args);
                }
                else
                {
                    MouseWheel?.Invoke(this, args);
                }

                if (args.Handled)
                    return (IntPtr)1; // swallow
            }
            else if (msg == WM_MOUSEHWHEEL)
            {
                int delta = (short)((data.mouseData >> 16) & 0xffff);
                var args = new MouseWheelEventArgs(delta, data.pt.x, data.pt.y);
                MouseHWheel?.Invoke(this, args);
                if (args.Handled)
                    return (IntPtr)1; // swallow
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMouseHook] HookCallback error: {ex.Message}");
        }
        
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        try
        {
            Uninstall();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMouseHook] Dispose error: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely marshals hook data from the lParam pointer.
    /// </summary>
    private static bool TryGetHookData(IntPtr lParam, out MSLLHOOKSTRUCT data)
    {
        try
        {
            data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMouseHook] Failed to marshal hook data: {ex.Message}");
            data = default;
            return false;
        }
    }

    /// <summary>
    /// Checks if the mouse is currently hovering over any taskbar.
    /// Performance optimized with caching to reduce Windows API calls.
    /// </summary>
    private static bool IsMouseOverTaskbar(int x, int y)
    {
        try
        {
            // Quick sanity check - if coordinates are invalid, return false
            if (x < -32768 || x > 32767 || y < -32768 || y > 32767)
                return false;

            var pt = new POINT { x = x, y = y };
            var hwnd = WindowFromPoint(pt);
            
            if (hwnd == IntPtr.Zero)
                return false;

            // Use safe, cached comparison to check for known taskbar windows
            // Instead of GetClassName which can be slow/problematic, use window handle comparison
            return IsKnownTaskbarWindow(hwnd);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalMouseHook] IsMouseOverTaskbar error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a window handle refers to a known taskbar window.
    /// Uses cached taskbar handles to minimize Windows API calls.
    /// </summary>
    private static bool IsKnownTaskbarWindow(IntPtr hwnd)
    {
        try
        {
            long now = Environment.TickCount64;
            
            // Refresh cached handles if cache is stale
            if (now - _lastTaskbarCheckTick >= TASKBAR_CACHE_MS)
            {
                _lastTaskbarCheckTick = now;
                _cachedPrimaryTaskbar = FindWindow("Shell_TrayWnd", null);
                _cachedSecondaryTaskbar = FindWindow("Shell_SecondaryTrayWnd", null);
            }

            // Compare against cached handles
            if (_cachedPrimaryTaskbar != IntPtr.Zero && hwnd == _cachedPrimaryTaskbar)
                return true;

            if (_cachedSecondaryTaskbar != IntPtr.Zero && hwnd == _cachedSecondaryTaskbar)
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cached version of IsShiftPressed to reduce API calls.
    /// Results are cached for KEY_STATE_CACHE_MS milliseconds.
    /// </summary>
    private bool IsShiftPressedCached()
    {
        long now = Environment.TickCount64;
        
        // If cache is still valid, return cached value
        if (now - _lastKeyStateCheckTick < KEY_STATE_CACHE_MS)
            return _cachedShiftState;

        // Update cache
        _lastKeyStateCheckTick = now;
        _cachedShiftState = IsShiftPressed();
        return _cachedShiftState;
    }

    private static bool IsShiftPressed()
    {
        const int VK_SHIFT = 0x10;
        try
        {
            short state = GetAsyncKeyState(VK_SHIFT);
            return (state & 0x8000) != 0;
        }
        catch
        {
            return false;
        }
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public int mouseData;
        public int flags;
        public int time;
        public nint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
}

