using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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

public static class FreedeckAppRunner
{
    private static bool _appRunning;
    private static Process? _node;
    private static Process? _electron;
    private static AppLaunchType? _currentlyRunning;
    public static bool KillMode;

    public static bool ReallyCheckIfAppIsRunning()
    {
        try
        {
            bool nodeRunning = _node != null && !_node.HasExited;
            bool electronRunning = _electron != null && !_electron.HasExited;
            bool anyProcessRunning = nodeRunning || electronRunning;

            MainWindow.Log("FAR>ReallyCheckIfAppIsRunning", $"Node running = {nodeRunning}");
            MainWindow.Log("FAR>ReallyCheckIfAppIsRunning", $"Electron running = {electronRunning}");
            MainWindow.Log("FAR>ReallyCheckIfAppIsRunning", $"_appRunning flag = {_appRunning}");
            MainWindow.Log("FAR>ReallyCheckIfAppIsRunning", $"Any process running = {anyProcessRunning}");
        
            if (!anyProcessRunning)
            {
                _appRunning = false;
            }
        
            return _appRunning && anyProcessRunning;
        }
        catch (Exception ex)
        {
            MainWindow.Log("FAR>ReallyCheckIfAppIsRunning", $"Error checking processes: {ex.Message}");
            _appRunning = false;
            return false;
        }
    }
    
    public static bool[] ReallyCheckIfAppIsRunningList()
    {
        try
        {
            bool nodeRunning = _node != null && !_node.HasExited;
            bool electronRunning = _electron != null && !_electron.HasExited;
        
            return [nodeRunning, electronRunning];
        }
        catch (Exception ex)
        {
            MainWindow.Log("FAR>ReallyCheckIfAppIsRunningList", $"Error checking process list: {ex.Message}");
            return [false, false];
        }
    }

    public static void KillAllProcesses()
    {
        try
        {
            _node?.Kill();
            _electron?.Kill();
            _node = null;
            _electron = null;
        }
        catch (Exception e)
        {
            Console.WriteLine("KAP: " + e.Message);
            Console.WriteLine("Exiting anyways, since we're already there.");
            Process.GetCurrentProcess().Kill();
        }
    }
    
    private static void StartInternalProgram(string args, InternalAppType type)
    {
        var useElectron = type == InternalAppType.Electron;
        
        var proc = new Process();
        if (useElectron)
            _electron = proc;
        else
            _node = proc;

        var showingTerminal = LauncherConfig.Configuration.ShowTerminal || useElectron;
        proc.StartInfo.WindowStyle = showingTerminal ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden;
        proc.StartInfo.Arguments = " src\\index.js " + args;
        proc.StartInfo.WorkingDirectory = MainWindow.InstallPath + "\\freedeck";
        
        if (useElectron)
            proc.StartInfo.FileName = MainWindow.InstallPath + "\\freedeck\\node_modules\\electron\\dist\\electron.exe";
        else
            proc.StartInfo.FileName = LauncherConfig.Configuration.NodePath;

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
            proc.OutputDataReceived += (_, dataReceivedEventArgs) => StdoutLog(type, InternalLogType.Out, dataReceivedEventArgs.Data!);
            proc.ErrorDataReceived += (_, dataReceivedEventArgs) => StdoutLog(type, InternalLogType.Err, dataReceivedEventArgs.Data!);
        }

        proc.Start();
        
        if (showingTerminal) return;
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
    }

    private static void StdoutLog(InternalAppType type, InternalLogType logType, string logMessage)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Console.WriteLine($"[{(type == InternalAppType.Node ? "SERVER" : "COMPANION")} {(logType == InternalLogType.Out ? "OUT" : "ERR")}] {logMessage}");
        });
    }

    private static void Process_ServerExit(object? sender, EventArgs e)
    {
        try
        {
            try
            {
                Dispatcher.UIThread.InvokeAsync(LauncherPersonalization.Initialize);
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (KillMode) return;
                    MainWindow.Instance.TabHandoff.IsVisible = false;
                    MainWindow.Instance.TabRun.IsSelected = true;
                    MainWindow.Instance.TabSettings.IsEnabled = true;
                    MainWindow.Instance.TabRun.IsEnabled = true;
                });
                if (_currentlyRunning != AppLaunchType.Server)
                {
                    _electron?.Kill();
                    _electron = null;
                }
            }
            catch (Exception error)
            {
                StdoutLog(InternalAppType.Node, InternalLogType.Err, $"Couldn't kill Electron.\n{error}");
            }

            BringBack();
        }
        catch (Exception ex)
        {
            StdoutLog(InternalAppType.Node, InternalLogType.Err, $"Couldn't kill Electron (Main loop).\n{ex}");
        }
    }

    private static void Process_ElectronExit(object? sender, EventArgs e)
    {
        try
        {
            Dispatcher.UIThread.InvokeAsync(LauncherPersonalization.Initialize);
            if (_currentlyRunning != AppLaunchType.Companion)
            {
                _node?.Kill();
                _node = null;
            }
            _appRunning = false;
        }
        catch (Exception ex)
        {
            StdoutLog(InternalAppType.Electron, InternalLogType.Err, $"Couldn't kill Node server.{ex}");
        }

        BringBack();
    }

    private static void BringBack()
    {
        _appRunning = false;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.Show();
            MainWindow.Instance.WindowState = WindowState.Normal;
            MainWindow.Instance.Activate();
            MainWindow.Instance.Focus();
            SetProgress("", 0);
        });
    }

    private static void SetProgress(string text, int progress)
    {
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarCurrently.Text = text;
            MainWindow.Instance.ProgressBarApp.Value = progress;
        });
    }

    public static void StartFreedeck(AppLaunchType type)
    {
        MainWindow.Log("FreedeckAppRunner", "Starting Freedeck with launch type " + type);
        if (_appRunning)
        {
            _appRunning = false;
            MainWindow.Instance.LaunchApp.IsEnabled = false;
            MainWindow.LauncherVersion = "Stopping Freedeck...";
            _node?.Kill();
            _electron?.Kill();
            _node = null;
            _electron = null;
            MainWindow.Instance.LaunchApp.IsEnabled = true;
            MainWindow.GetAndSetVersionData();
            Console.WriteLine("Stopped Freedeck.");
            return;
        }
        _currentlyRunning = type;

        Dispatcher.UIThread.InvokeAsync(() => MainWindow.Instance.LaunchApp.IsEnabled = true);
        if (type != AppLaunchType.Companion)
        {
            SetProgress("Starting Server... [1/2]", 30);
            _ = Task.Run(() => { StartInternalProgram("--server-only", InternalAppType.Node); });
        }

        if (type != AppLaunchType.Server)
        {
            SetProgress("Starting Companion... [2/2]", 40);
            _ = Task.Run(() => { StartInternalProgram("--companion-only", InternalAppType.Electron); });
        }

        Dispatcher.UIThread.InvokeAsync(() => MainWindow.Instance.LaunchApp.IsEnabled = true);
        _appRunning = true;
        SetProgress("Here we go!", 100);
        if (type == AppLaunchType.Server) // Server ONLY mode
        {
            SetProgress("Server is running!", 100);
        }
        else 
            Dispatcher.UIThread.InvokeAsync(() => MainWindow.Instance.Hide());
    }
}