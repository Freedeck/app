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
    public string internetUrl { get; set; }
    public string pathToSaveTo { get; set; }
    public string expected { get; set; }
    public string initialMessage { get; set; }
    public string message { get; set; }
    public Action callback { get; set; }
}

public class FreedeckAppInstaller
{
    private string folder = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
    public void InstallIt(Action callback)
    {
        MainWindow.Instance.InstallState.Text = "Creating folder...";
        if (!Directory.Exists(MainWindow.InstallPath))
        {
            Directory.CreateDirectory(MainWindow.InstallPath);
        }
        SetupLogic.CopyLauncherToInstallation();
        if(SetupLogic.IsChecked(MainWindow.Instance.SaSDesktop)) AppShortcutToDesktop("Freedeck", MainWindow.InstallPath +"\\Freedeck.exe");
        if(SetupLogic.IsChecked(MainWindow.Instance.SaSStart)) AppShortcutToDesktop("Freedeck", MainWindow.InstallPath +"\\Freedeck.exe", Environment.SpecialFolder.StartMenu);
        LauncherConfig.ReloadConfiguration();
        MainWindow.Instance.InstallProgress.Value = 15;
        
        MainWindow.Instance.InstallState.Text = "Starting downloads...";
        MainWindow.Instance.InstallProgress.Value = 20;
        FreedeckAppSchema Node = new FreedeckAppSchema
        {
            internetUrl = "https://nodejs.org/dist/v20.15.0/node-v20.15.0-x64.msi",
            pathToSaveTo = folder + "\\fd_node_install.msi",
            expected = "C:\\Program Files\\nodejs\\node.exe",
            initialMessage = "Checking for node...",
            message = "Please follow the Node.js installer's instructions!",
            callback = () =>
            {
                StageTwo(callback);
            }
        };
        FreedeckAppSchema Git = new FreedeckAppSchema
        {
            internetUrl =
                "https://github.com/git-for-windows/git/releases/download/v2.47.0.windows.1/Git-2.47.0-64-bit.exe",
            pathToSaveTo = folder + "\\fd_git_install.exe",
            expected = "C:\\Program Files\\Git\\bin\\git.exe",
            initialMessage = "Checking for git...",
            message = "Please follow the git installer's instructions!",
            callback = MakeUserInstall(Node)
        };
        MakeUserInstall(Git)();
    }
    
    private void StageTwo(Action finish)
    {
        MainWindow.Instance.InstallProgress.Value = 30;
        MainWindow.Instance.InstallState.Text = "Fetching Git repo (github: freedeck/freedeck)";
        Process proc = new Process();
        proc.StartInfo.FileName = "C:\\Program Files\\Git\\bin\\git.exe";
        proc.StartInfo.ArgumentList.Add("clone");
        proc.StartInfo.ArgumentList.Add("https://github.com/freedeck/freedeck");
        proc.StartInfo.ArgumentList.Add(MainWindow.InstallPath + "\\freedeck");
        proc.EnableRaisingEvents = true;
        proc.Exited += ((sender, args) =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                StageThree(finish);
            });
        });
        proc.Start();
    }

    private void StageThree(Action finish)
    {
        MainWindow.Instance.InstallProgress.Value = 40;
        MainWindow.Instance.InstallState.Text = "Cloned Git repo! Installing dependencies...";
        Process proc = new Process();
        proc.StartInfo.FileName = "C:\\Program Files\\nodejs\\npm.cmd";
        proc.StartInfo.UseShellExecute = true;
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
    public static void AppShortcutToDesktop(string linkName, string app, Environment.SpecialFolder folder = Environment.SpecialFolder.DesktopDirectory)
    {
        string deskDir = Environment.GetFolderPath(folder);

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
            MakeUserInstall(
                schema.internetUrl, 
                schema.pathToSaveTo,
                schema.expected,
                schema.initialMessage,
                schema.message,
                schema.callback);
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