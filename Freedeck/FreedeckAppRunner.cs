using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Freedeck;

public enum AppLaunchType
{
    All,
    Server,
    Companion
}

public enum InternalAppType
{
    Node,
    Electron,
}

public enum InternalLogType
{
    Err,
    Out
}

public class FreedeckAppRunner
{
    private bool _appRunning = false;
    private Process _node = null!;
    private Process _electron = null!;
    private AppLaunchType _currentlyRunning;
    
    private void StartInternalProgram(string args, InternalAppType type)
    {
        bool useElectron = type == InternalAppType.Electron;
        Process proc = new Process();
        if (useElectron)
            _electron = proc;
        else
            _node = proc;

        bool showingTerminal = LauncherConfig.Configuration.ShowTerminal || useElectron;
        proc.StartInfo.WindowStyle = showingTerminal ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;
        proc.StartInfo.Arguments = " src\\index.js " + args;
        proc.StartInfo.WorkingDirectory = MainWindow.InstallPath + "\\freedeck";
        
        if (useElectron)
            proc.StartInfo.FileName = MainWindow.InstallPath + "\\freedeck\\node_modules\\electron\\dist\\electron.exe";
        else
            proc.StartInfo.FileName = "C:\\Program Files\\nodejs\\node.exe";

        proc.EnableRaisingEvents = true;
        
        if (useElectron)
            proc.Exited += Process_ElectronExit;
        else
            proc.Exited += Process_ServerExit;

        if (!showingTerminal)
        {
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.OutputDataReceived += (sender, args) => StdoutLog(type, InternalLogType.Out, args.Data!);
            proc.ErrorDataReceived += (sender, args) => StdoutLog(type, InternalLogType.Err, args.Data!);
        }

        proc.Start();
        if (!showingTerminal)
        {
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
        }
    }

    private void StdoutLog(InternalAppType type, InternalLogType logType, string logMessage)
    {
        String fmt = $"[{(type == InternalAppType.Node ? "SERVER" : "COMPANION")} {(logType == InternalLogType.Out ? "OUT" : "ERR")}] {logMessage}";
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Console.WriteLine(fmt);
        });
    }

    private void Process_ServerExit(object? sender, EventArgs e)
    {
        try
        {
            if (File.Exists(MainWindow.InstallPath + "\\freedeck\\src\\configs\\config.fd.js"))
            {
                try
                {
                    Dispatcher.UIThread.InvokeAsync(LauncherPersonalization.Initialize);
                    if(_currentlyRunning != AppLaunchType.Server) _electron.Kill();
                }
                catch (Exception errr)
                {
                    StdoutLog(InternalAppType.Node, InternalLogType.Err, $"Couldn't kill Electron.\n{errr}");
                }

                BringBack();
            }
        }
        catch (Exception ex)
        {
            StdoutLog(InternalAppType.Node, InternalLogType.Err, $"Couldn't kill Electron (Main loop).\n{ex}");
        }
    }

    private void Process_ElectronExit(object? sender, EventArgs e)
    {
        try
        {
            Dispatcher.UIThread.InvokeAsync(LauncherPersonalization.Initialize);
            if(_currentlyRunning != AppLaunchType.Companion) _node.Kill();
            _appRunning = false;
        }
        catch (Exception ex)
        {
            StdoutLog(InternalAppType.Electron, InternalLogType.Err, $"Couldn't kill Node server.{ex}");
        }

        BringBack();
    }

    public void BringBack()
    {
        _appRunning = false;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.Show();
            MainWindow.Instance.WindowState = WindowState.Normal;
            MainWindow.Instance.Activate();
            MainWindow.Instance.Focus();
            MainWindow.Instance.ProgressBarContainer.IsVisible = false;
        });
    }

    public void SpecificStop()
    {
        
    }

    public void StartFreedeck(AppLaunchType type)
    {
        if (_appRunning)
        {
            _appRunning = false;
            MainWindow.Instance.LaunchApp.IsEnabled = false;
            MainWindow.LauncherVersion = "Stopping Freedeck...";
            if (!_node.HasExited) _node.Kill();
            if (!_electron.HasExited) _electron.Kill();
            MainWindow.Instance.LaunchApp.IsEnabled = true;
            MainWindow.GetAndSetVersionData();
            return;
        }
        _currentlyRunning = type;

        MainWindow.Instance.LaunchApp.IsEnabled = true;
        if (type != AppLaunchType.Companion)
        {
            MainWindow.Instance.ProgressBarCurrently.Text = "Starting Server... [1/2]";
            StartInternalProgram("--server-only", InternalAppType.Node);
            MainWindow.Instance.ProgressBarApp.Value = 30;
        }

        if (type != AppLaunchType.Server)
        {
            MainWindow.Instance.ProgressBarCurrently.Text = "Starting Companion... [2/2]";
            StartInternalProgram("--companion-only", InternalAppType.Electron);
            MainWindow.Instance.ProgressBarApp.Value = 40;
        }
        MainWindow.Instance.LaunchApp.IsEnabled = true;
        _appRunning = true;
        MainWindow.Instance.ProgressBarApp.Value = 100;
        MainWindow.Instance.ProgressBarCurrently.Text = "Here we go!";
        if (type == AppLaunchType.Server) // Server ONLY mode
        {
            MainWindow.Instance.ProgressBarCurrently.Text = "The server is running.";
        }
        else 
            MainWindow.Instance.Hide();
    }
}