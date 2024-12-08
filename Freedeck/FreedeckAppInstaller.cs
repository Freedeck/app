using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Freedeck.Fakedeck;

namespace Freedeck;

public class FreedeckAppSchema
{
    public string? InternetUrl { get; init; }
    public string? PathToSaveTo { get; init; }
    public string? Expected { get; init; }
    public string? InitialMessage { get; init; }
    public string? Message { get; init; }
    public Action? Callback { get; init; }
}

public class FreedeckAppInstaller
{
    private string folder = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
    public void InstallIt(Action callback)
    {
        MainWindow.Instance.AppInstallLogContainer.IsVisible = true;
        MainWindow.Instance.InstallState.Text = "Creating folder...";
        if(SetupLogic.IsChecked(MainWindow.Instance.SaSDesktop)) AppShortcutToDesktop("Freedeck", LauncherConfigSchema.AppData + "\\Freedeck.exe", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        if(SetupLogic.IsChecked(MainWindow.Instance.SaSStart)) AppShortcutToDesktop("Freedeck", LauncherConfigSchema.AppData + "\\Freedeck.exe", Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\Programs");
        LauncherConfig.ReloadConfiguration();
        MainWindow.Instance.InstallProgress.Value = 15;
        
        MainWindow.Instance.InstallState.Text = "Starting downloads...";
        MainWindow.Instance.InstallProgress.Value = 20;
        FreedeckAppSchema Node = new FreedeckAppSchema
        {
            InternetUrl = "https://nodejs.org/dist/v20.15.0/node-v20.15.0-x64.msi",
            PathToSaveTo = folder + "\\fd_node_install.msi",
            Expected = "C:\\Program Files\\nodejs\\node.exe",
            InitialMessage = "Checking for node...",
            Message = "Please follow the Node.js installer's instructions!",
            Callback = () =>
            {
                StageTwo(callback);
            }
        };
        FreedeckAppSchema Git = new FreedeckAppSchema
        {
            InternetUrl =
                "https://github.com/git-for-windows/git/releases/download/v2.47.0.windows.1/Git-2.47.0-64-bit.exe",
            PathToSaveTo = folder + "\\fd_git_install.exe",
            Expected = "C:\\Program Files\\Git\\bin\\git.exe",
            InitialMessage = "Checking for git...",
            Message = "Please follow the git installer's instructions!",
            Callback = MakeUserInstall(Node)
        };
        MakeUserInstall(Git)();
    }
    
    private void StdoutLog(string title, InternalLogType logType, string logMessage)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            String fmt = $"[{title}] {logMessage}";
            Console.WriteLine(fmt + logType);
            MainWindow.Instance.AppInstallLog.Text += fmt + "\n";
        });
    }
    private void StageTwo(Action finish)
    {
        MainWindow.Instance.InstallProgress.Value = 30;
        MainWindow.Instance.InstallState.Text = "Fetching Git repo (github: freedeck/freedeck)";
        Process proc = new Process();
        proc.StartInfo.FileName = "C:\\Program Files\\Git\\bin\\git.exe";
        proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.OutputDataReceived += (sender, args) => StdoutLog("Git", InternalLogType.Out, args.Data!);
        proc.ErrorDataReceived += (sender, args) => StdoutLog("Git", InternalLogType.Err, args.Data!);
        proc.StartInfo.ArgumentList.Add("clone");
        proc.StartInfo.ArgumentList.Add("https://github.com/freedeck/freedeck");
        proc.StartInfo.ArgumentList.Add(MainWindow.InstallPath + "\\freedeck");
        proc.StartInfo.ArgumentList.Add("-b");
        proc.StartInfo.ArgumentList.Add("v6" + (SetupLogic.IsChecked(MainWindow.Instance.SaRelease) ? "" : "-dev"));
        proc.EnableRaisingEvents = true;
        proc.Exited += ((sender, args) =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                StageThree(finish);
            });
        });
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
    }

    private void StageThree(Action finish)
    {
        MainWindow.Instance.InstallProgress.Value = 40;
        MainWindow.Instance.InstallState.Text = "Cloned Git repo! Installing dependencies...";
        Process proc = new Process();
        proc.StartInfo.FileName = "C:\\Program Files\\nodejs\\npm.cmd";
        proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.OutputDataReceived += (sender, args) => StdoutLog("NPM", InternalLogType.Out, args.Data!);
        proc.ErrorDataReceived += (sender, args) => StdoutLog("NPM", InternalLogType.Err, args.Data!);
        proc.StartInfo.ArgumentList.Add("i");
        proc.StartInfo.WorkingDirectory = MainWindow.InstallPath + "\\freedeck";
        proc.EnableRaisingEvents = true;
        proc.Exited += ((sender, args) =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                StageFour(finish);
            });
        });
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
    }

    private void StageFour(Action finish)
    {
        MainWindow.Instance.InstallProgress.Value = 85;
        MainWindow.Instance.InstallState.Text = "Making your configuration...";
        FakeConfig.CreateDefaultConfiguration();
        MainWindow.Instance.InstallProgress.Value = 100;
        MainWindow.Instance.InstallState.Text = "Installation is complete. Relinquishing control to Main.";
        finish();
    }

    // https://stackoverflow.com/questions/4897655/create-a-shortcut-on-desktop
    public static void AppShortcutToDesktop(string linkName, string app, String folder)
    {
        string deskDir = folder;
        
        using (StreamWriter writer = new StreamWriter(deskDir + "\\" + linkName + ".url"))
        {
            writer.WriteLine("[InternetShortcut]");
            writer.WriteLine("URL=" + app);
            writer.WriteLine("IconIndex=0");
            string icon = app.Replace('\\', '/');
            writer.WriteLine("IconFile=" + icon);
        }
    }
    
    private Action MakeUserInstall(FreedeckAppSchema schema)
    {
        return () =>
        {
            if (schema is not
                {
                    InternetUrl: not null, PathToSaveTo: not null, Expected: not null, InitialMessage: not null,
                    Message: not null, Callback: not null
                }) return;
            MakeUserInstall(
                schema.InternetUrl,
                schema.PathToSaveTo,
                schema.Expected,
                schema.InitialMessage,
                schema.Message,
                schema.Callback);
        };
    }

    private Action MakeUserInstall(string internetUrl, string pathToSaveTo, String expected, string initialMessage, string message, Action callback)
    {
        MainWindow.Instance.InstallState.Text = initialMessage;
        if (File.Exists(pathToSaveTo) || File.Exists(expected))
        {
            callback();
            return () => { };
        };
        HttpClient hc = new HttpClient();
        Uri uri = new Uri(internetUrl);

        async void Action()
        {
            var response = await hc.GetAsync(uri);
            await using (var fs = new FileStream(pathToSaveTo, FileMode.CreateNew))
            {
                await response.Content.CopyToAsync(fs);
            }

            MainWindow.Instance.InstallState.Text = message;
            Process proc = new Process();
            proc.StartInfo.WorkingDirectory = folder;
            proc.StartInfo.FileName = pathToSaveTo;
            proc.EnableRaisingEvents = true;
            proc.Exited += (sender, args) => { Dispatcher.UIThread.InvokeAsync(() => callback); };
            proc.Start();
        }

        _ = new Task(Action);
        return () => { };
    }

    private void ProcessExited(object? sender, EventArgs e)
    {
        throw new NotImplementedException();
    }
}