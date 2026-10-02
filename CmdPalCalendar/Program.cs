using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace CmdPalCalendar;

public static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0 || args[0] != "-RegisterProcessAsComServer")
        {
            Console.WriteLine("Not being launched as an extension... exiting.");
            return;
        }

        using var disposed = new ManualResetEvent(false);
        var extension = new CalendarExtension(disposed);

        var server = new ComServer();
        server.RegisterClass<CalendarExtension, IExtension>(() => extension);
        server.Start();

        disposed.WaitOne();
        server.Stop();
        server.UnsafeDispose();
    }
}
