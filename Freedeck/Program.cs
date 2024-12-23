﻿using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Freedeck;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    
    private static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows()) return false;
        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }


    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    [STAThread]
    public static void Main(string[] args) {
        // AllocConsole();
        string protocol = "freedeck";  // Replace with your protocol name
        var mainModuleFileName = Process.GetCurrentProcess().MainModule?.FileName;
        if (mainModuleFileName != null)
        {
            using var mutex = new System.Threading.Mutex(true, "Global\\fd_app_inst", out var createdNew);
            if (!createdNew)
            {
                if (args.Length > 0 && args[0].Contains("freedeck://"))
                {
                    SendArgsToExistingInstance(args);
                }
                else if (args.Length > 0 && args[0].Contains("HandoffAdminReset") && OperatingSystem.IsWindows())
                {
                    if (!IsAdministrator()) return;
                    UriProtocolRegistrar.RegisterUriScheme(protocol, mainModuleFileName, false);
                }
            } else {
                _ = Task.Run(() =>
                {
                    UriProtocolRegistrar.RegisterUriScheme(protocol, mainModuleFileName, userLevel: true);
                });
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
        }

    }

    private static void SendArgsToExistingInstance(string[] args)
    {
        using var pipeClient = new System.IO.Pipes.NamedPipeClientStream(".", "fd_app_handoff", 
            System.IO.Pipes.PipeDirection.Out);
        pipeClient.Connect();
        using var writer = new StreamWriter(pipeClient);
        writer.AutoFlush = true;
        writer.WriteLine(string.Join(" ", args));  // Send the URI to the existing instance.
        writer.Close();
        Process.GetCurrentProcess().Kill();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}