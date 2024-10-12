using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;

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

    [STAThread]
    public static void Main(string[] args) {
        
        string protocol = "freedeck";  // Replace with your protocol name
        string appPath = Process.GetCurrentProcess().MainModule.FileName;

        // Try registering without admin rights (user-level)
        UriProtocolRegistrar.RegisterUriScheme(protocol, appPath, userLevel: true);
        
        var mutex = new System.Threading.Mutex(true, "FreedeckAppMutex", out bool isNewInstance);
        if (!isNewInstance)
        {
            // The app is already running. Pass the arguments to the running instance.
            if(args[0].Contains("freedeck://")) SendArgsToExistingInstance(args);
            else if(args[0].Contains("HandoffAdminReset") && OperatingSystem.IsWindows())
            {
                if (!IsAdministrator()) return;
                UriProtocolRegistrar.RegisterUriScheme(protocol, appPath, false);                
            }
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void SendArgsToExistingInstance(string[] args)
    {
        using var pipeClient = new System.IO.Pipes.NamedPipeClientStream(".", "FreedeckAppHandoff", 
            System.IO.Pipes.PipeDirection.Out);
        pipeClient.Connect();
        using var writer = new StreamWriter(pipeClient);
        writer.AutoFlush = true;
        writer.WriteLine(string.Join(" ", args));  // Send the URI to the existing instance.
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}