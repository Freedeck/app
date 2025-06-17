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

    public void CloseApp(object? sender, EventArgs eventArgs)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.Close();
        });
    }
    
    private async void StartListening()
    {
        MainWindow.Log("HandoffPipe", "Listening for commands on 'fd_app_handoff'.");
        while (true)
        {
            try
            {
                await using var pipeServer = new NamedPipeServerStream("fd_app_handoff", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipeServer.WaitForConnectionAsync();
                using var reader = new StreamReader(pipeServer);
                var uri = await reader.ReadLineAsync() ?? string.Empty;  // Read the message sent by the new instance.

                if (string.IsNullOrWhiteSpace(uri)) continue;
                
                MainWindow.Log("HandoffPipe", $"Received command {uri}, handling...");
                HandleUri(uri);
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
            Task.Run(() =>
            {
                _ = Task.Run(StartListening);
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}