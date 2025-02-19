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

public class FreedeckAppRunner
{
    private static bool _appRunning = false;
    private static Process _node = null;
    private static Process _electron = null;
    private static AppLaunchType _currentlyRunning;

    public static bool IsAppRunning()
    {
        return _appRunning;
    }

    public static bool ReallyCheckIfAppIsRunning()
    {
        var otherChecks = false;
        var pname = Process.GetProcessesByName("node");
        var pname2 = Process.GetProcessesByName("electron");
        if (pname.Length > 0 || pname2.Length > 0)
        {
            otherChecks = true;
        }
        
        var hasExitedCheck =  (_node is { HasExited: true } || _electron is { HasExited: true });
        return (_appRunning && !hasExitedCheck) || otherChecks;
    }
    
    public static bool[] ReallyCheckIfAppIsRunningList()
    {
        var fdServer = GetFdProcesses(Process.GetProcessesByName("node"))[0];
        var fdCompanion = GetFdProcesses(Process.GetProcessesByName("electron"))[0];
        
        return [fdServer != null, fdCompanion != null];
    }

    public static void KillAllProcesses()
    {
        var fdServer = GetFdProcesses(Process.GetProcessesByName("node"));
        var fdCompanion = GetFdProcesses(Process.GetProcessesByName("electron"));
        foreach(var p in fdServer) p.Kill();
        foreach(var p in fdCompanion) p.Kill();
    }

    public static List<Process> GetFdProcesses(Process[] ps)
    {
        var n = new List<Process>();
        foreach (var p in ps)
        {
            if (!p.StartInfo.WorkingDirectory.Contains("freedeck")) continue;
            n.Add(p);
            break;
        }

        return n;
    }
    
    public static Task ProbeAndAttachRunningInstancesOfFreedeck()
    {
        var fdServer = GetFdProcesses(Process.GetProcessesByName("node"))[0];
        var fdCompanion = GetFdProcesses(Process.GetProcessesByName("electron"))[0];
        
        if (fdServer != null && fdCompanion != null)
        {
            _node = fdServer;
            _electron = fdCompanion;
            _appRunning = true;
            _currentlyRunning = AppLaunchType.All;
        }
        else if (fdServer != null)
        {
            _node = fdServer;
            _appRunning = true;
            _currentlyRunning = AppLaunchType.Server;
        }
        else if (fdCompanion != null)
        {
            _electron = fdCompanion;
            _appRunning = true;
            _currentlyRunning = AppLaunchType.Companion;
        }
        
        Console.WriteLine("Probed - Node: " + fdServer + " Electron: " + fdCompanion);
        return Task.CompletedTask;
    }
    
    private static void StartInternalProgram(string args, InternalAppType type)
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

    private static void StdoutLog(InternalAppType type, InternalLogType logType, string logMessage)
    {
        String fmt = $"[{(type == InternalAppType.Node ? "SERVER" : "COMPANION")} {(logType == InternalLogType.Out ? "OUT" : "ERR")}] {logMessage}";
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Console.WriteLine(fmt);
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
                    MainWindow.Instance.TabHandoff.IsVisible = false;
                    MainWindow.Instance.TabRun.IsSelected = true;
                    MainWindow.Instance.TabSettings.IsEnabled = true;
                    MainWindow.Instance.TabRun.IsEnabled = true;
                });
                if(_currentlyRunning != AppLaunchType.Server) _electron.Kill();
            }
            catch (Exception errr)
            {
                StdoutLog(InternalAppType.Node, InternalLogType.Err, $"Couldn't kill Electron.\n{errr}");
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
            if(_currentlyRunning != AppLaunchType.Companion) _node.Kill();
            _appRunning = false;
        }
        catch (Exception ex)
        {
            StdoutLog(InternalAppType.Electron, InternalLogType.Err, $"Couldn't kill Node server.{ex}");
        }

        BringBack();
    }

    public static void BringBack()
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

    public static void SetProgress(string text, int progress)
    {
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarCurrently.Text = text;
            MainWindow.Instance.ProgressBarApp.Value = progress;
        });
    }

    public static async void StartFreedeck(AppLaunchType type)
    {
        MainWindow.Log("FreedeckAppRunner", "Starting Freedeck with launch type " + type);
        if (_appRunning)
        {
            _appRunning = false;
            MainWindow.Instance.LaunchApp.IsEnabled = false;
            MainWindow.LauncherVersion = "Stopping Freedeck...";
            if (!_node.HasExited) _node.Kill();
            if (!_electron.HasExited) _electron.Kill();
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