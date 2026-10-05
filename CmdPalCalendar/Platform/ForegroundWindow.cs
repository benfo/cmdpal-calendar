using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CmdPalCalendar.Platform;

internal static class ForegroundWindow
{
    private const int Attempts = 10;
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(100);

    public static IntPtr Current() => NativeMethods.GetForegroundWindow();

    // A closing sign-in window hands focus to the previous window once it's gone, so keep
    // asking for a moment instead of once.
    public static async Task RestoreAsync(IntPtr window, Action<string> reportFailure)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            await Task.Delay(Pause);
            if (NativeMethods.GetForegroundWindow() == window)
            {
                return;
            }

            BringToFront(window);
        }

        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground != window)
        {
            reportFailure($"wanted {Describe(window)}, got {Describe(foreground)}");
        }
    }

    // Windows only lets the foreground process hand on focus, so join the foreground
    // thread's input briefly when the plain call is refused.
    private static void BringToFront(IntPtr window)
    {
        if (NativeMethods.SetForegroundWindow(window))
        {
            return;
        }

        var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        var currentThread = NativeMethods.GetCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == currentThread)
        {
            return;
        }

        NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            NativeMethods.SetForegroundWindow(window);
        }
        finally
        {
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    private static string Describe(IntPtr window)
    {
        var title = new char[256];
        var className = new char[256];
        var titleLength = NativeMethods.GetWindowText(window, title, title.Length);
        var classLength = NativeMethods.GetClassName(window, className, className.Length);
        return $"\"{new string(title, 0, titleLength)}\" ({new string(className, 0, classLength)}, {window})";
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool doAttach);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr window, [Out] char[] text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr window, [Out] char[] className, int maxCount);
    }
}
