using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Freedeck;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    private async void StartListening()
    {
        Console.WriteLine("Handoff pipe listening!");
        while (true)
        {
            try
            {
                using var pipeServer = new NamedPipeServerStream("fd_app_handoff", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipeServer.WaitForConnectionAsync();
                using var reader = new StreamReader(pipeServer);
                string uri = await reader.ReadLineAsync() ?? string.Empty;  // Read the message sent by the new instance.
            
                if (!string.IsNullOrWhiteSpace(uri))
                {
                    Console.WriteLine($"Received URI: {uri}");
                    HandleUri(uri);  // Handle the URI command (e.g., bring the window to the front).
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Pipe server error: {ex.Message}");
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static void BringToTop()
    {
        IntPtr handle = (IntPtr)TopLevel.GetTopLevel(MainWindow.Instance)?.TryGetPlatformHandle()?.Handle!;
        SetForegroundWindow(handle);
    }
    
    private void HandleUri(string uri)
    {
        Console.WriteLine($"Received URI: {uri}");
        BringToTop();
        Dispatcher.UIThread.InvokeAsync(() =>
        { 
            MainWindow.Instance.Show();
            HandoffHelper.HandleCommand(uri);
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            if (desktop.Args!.Length > 0 && desktop.Args[0].Contains("freedeck://startup"))
            {
                desktop.MainWindow.Hide();
            }
            _ = Task.Run(StartListening);
        }

        base.OnFrameworkInitializationCompleted();
    }
}