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

    static bool AnotherInstanceRunning() 
    {
        return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1;
    }
    
    [STAThread]
    public static void Main(string[] args) {
        AllocConsole();
        var protocol = "freedeck";  // Replace with your protocol name
        var mainModuleFileName = Process.GetCurrentProcess().MainModule?.FileName;
        if (mainModuleFileName == null) return;
        if (args.Length > 0 && args[0].Contains("HandoffAdminReset") && OperatingSystem.IsWindows())
        {
            if (!IsAdministrator()) return;
            UriProtocolRegistrar.RegisterUriScheme(protocol, mainModuleFileName, false);
        }
        if (AnotherInstanceRunning())
        {
            switch (args.Length)
            {
                case 0:
                    SendArgsToExistingInstance(["freedeck://native_open"]);
                    break;
                case > 0 when args[0].Contains("freedeck://"):
                    SendArgsToExistingInstance(args);
                    break;
            }
        } else {
            _ = Task.Run(() =>
            {
                UriProtocolRegistrar.RegisterUriScheme(protocol, mainModuleFileName, userLevel: true);
            });
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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