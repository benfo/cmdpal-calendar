using System;
using System.Runtime.InteropServices;

namespace CmdPalCalendar.Platform;

internal static class ForegroundWindow
{
    public static IntPtr Current() => NativeMethods.GetForegroundWindow();

    // Windows only lets the foreground process take focus; the sign-in window's process held it,
    // so join the current foreground thread's input briefly when the plain call is refused.
    public static void Restore(IntPtr window)
    {
        if (window == IntPtr.Zero || NativeMethods.SetForegroundWindow(window))
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
    }
}
