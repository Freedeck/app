using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Freedeck;

public class SetupLogic
{
    public SetupLogic() { }

    private void ToggleAuthenticationEvent(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch)
        {
            MainWindow.Instance.SaAuthenticationPassword.IsVisible = (bool)MainWindow.Instance.SaAuthentication.IsChecked!;
            MainWindow.Instance.SadAuthentication.Text = "Authentication: "+ContentOf(MainWindow.Instance.SaAuthentication);
        }
    }

    private static object? ContentOf(ToggleSwitch toggleSwitch)
    {
        return ((toggleSwitch.IsChecked ?? false) ? toggleSwitch.OnContent : toggleSwitch.OffContent);
    }
    
    public static bool IsChecked(ToggleSwitch toggleSwitch)
    {
        return toggleSwitch.IsChecked ?? false;
    }

    public static void CopyLauncherToInstallation()
    {
        var currentProcess = Process.GetCurrentProcess();
        if (currentProcess?.MainModule != null)
        {
            string exeName = currentProcess.MainModule.FileName;
            string executableName = Path.GetFileName(exeName);
            string sourceExePath = Path.Combine(Environment.CurrentDirectory, executableName);
            Console.WriteLine("Launching copy task.");
            _ = Task.Run(() =>
            {
                Console.WriteLine($"Copying from {sourceExePath} to {LauncherConfigSchema.AppData}\\Freedeck.exe");
                File.Copy(sourceExePath, LauncherConfigSchema.AppData + "\\Freedeck.exe", true);
                Console.WriteLine("Copied launcher to installation directory.");
            });
        }
    }

    public void OnLaunch(MainWindow window)
    {
        window.Title = "Freedeck Installer";
        window.ILauncherVersion.IsVisible = false;
        window.TabRun.IsVisible = false;
        window.TabSettings.IsVisible = false;
        window.TabInstall.IsVisible = false;
        window.TabInstall.IsSelected = true;
        window.ITabInstall.IsSelected = true;
        window.AppInstallLogContainer.IsVisible = false;
        if (string.IsNullOrEmpty(MainWindow.InstallPath))
        {
            MainWindow.InstallPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Freedeck";
            LauncherConfig.Configuration.InstallationDirectory = MainWindow.InstallPath;
            LauncherConfig.Update();
        }
        if (!Directory.Exists(LauncherConfigSchema.AppData)) Directory.CreateDirectory(LauncherConfigSchema.AppData);
        CopyLauncherToInstallation();
        
        window.InstallationDirectory.Text = MainWindow.InstallPath;
        window.SadDirectory.Text = "Installing to " +  window.InstallationDirectory.Text;
        
        window.SaAuthentication.IsCheckedChanged += ToggleAuthenticationEvent;
        window.SaWelcome.IsCheckedChanged += ToggleWelcomeEvent;
        window.SaRelease.IsCheckedChanged += ToggleReleaseEvent;

        window.SNodePath.Text = LauncherConfig.Configuration.NodePath;
        window.SNpmPath.Text = LauncherConfig.Configuration.NpmPath;
        window.SGitPath.Text = LauncherConfig.Configuration.GitPath;
        
        window.InstallPathBtn.Click += DirectoryChangedEvent;
        window.NodePathBtn.Click += NodePathBtnOnClick;
        window.NpmPathBtn.Click += NpmPathBtnOnClick;
        window.GitPathBtn.Click += GitPathBtnOnClick;

        window.SaveConfigurationNow.Click += (sender, args) =>
        {
            LauncherConfig.Update();
        };
        
        window.SaNext.Click += (sender, args) =>
        {
            window.InstallerNest.SelectedIndex++;
        };

        window.SaMigrate.Click += (sender, args) =>
        {
            window.AppInstallLogContainer.IsVisible = true;
            window.ITabSetup.IsVisible = false;
            window.ITabInstall.IsSelected = true;
            window.ITabInstallTxt.Text = "Updating to new launcher...";
            window.InstallerBtn.IsVisible = false;
            window.ITabInstallDesc.Text = "We're upgrading Freedeck for you. This may take a second.";
            window.InstallState.IsVisible = true;
            window.InstallProgress.Value = 5;
            window.InstallState.Text = "Checking to ensure Freedeck exists here";
            bool continueInstallation = true;
            if (MigrateEnsureExists(window.InstallationDirectory.Text))
            {
                window.InstallState.IsVisible = false;
                window.InstallerBtn.IsVisible = true;
                window.ITabInstallTxt.Text = "Install";
                window.ITabInstallDesc.Text = "We could not find an existing installation.";
                window.ITabSetup.IsVisible = true;
                window.InstallProgress.Value = 0;
                continueInstallation = false;
            }

            if (continueInstallation)
            {

                window.InstallProgress.Value = 10;

                window.InstallState.Text = "Replacing launcher with this one...";

                CopyLauncherToInstallation();
                
                window.InstallProgress.Value = 15;
                window.InstallState.Text = "Cleaning up old launcher...";
                if (File.Exists(MainWindow.InstallPath + "\\launcher.exe"))
                {

                    File.Delete(MainWindow.InstallPath+ "\\launcher.exe");
                    window.InstallState.Text = "Deleted old launcher.";
                }
                if (File.Exists(MainWindow.InstallPath + "\\handoff.exe"))
                {
                    window.InstallState.Text = "Please allow admin, we're removing the old Handoff. We will not need admin after this.";
                    var exeName = Process.GetCurrentProcess().MainModule!.FileName;
                    ProcessStartInfo startInfo = new ProcessStartInfo(exeName);
                    startInfo.Verb = "runas";
                    startInfo.ArgumentList.Add("HandoffAdminReset");
                    Process.Start(startInfo);
                    File.Delete(MainWindow.InstallPath+"\\handoff.exe");
                    window.InstallState.Text = "Deleted old Handoff.";
                }
                
                window.InstallProgress.Value = 30;
                window.InstallState.Text = "Making new shortcuts!";
                if(SetupLogic.IsChecked(MainWindow.Instance.SaSDesktop)) FreedeckAppInstaller.AppShortcutToDesktop("Freedeck", LauncherConfigSchema.AppData + "\\Freedeck.exe", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                if(SetupLogic.IsChecked(MainWindow.Instance.SaSStart)) FreedeckAppInstaller.AppShortcutToDesktop("Freedeck", LauncherConfigSchema.AppData + "\\Freedeck.exe", Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\Programs");                window.InstallProgress.Value = 70;
                window.InstallState.Text = "Setting up configuration";
                LauncherConfig.Configuration.InstallationDirectory = window.InstallationDirectory.Text;
                LauncherConfig.Update();
                window.InstallProgress.Value = 90;
                window.InstallState.Text = "Done!! Fixing UI!";
                MainWindow.InstallPath = window.InstallationDirectory.Text;
                MainWindow.GetAndSetVersionData();
                window.Title = "Freedeck";
                window.TabInstall.IsVisible = false;
                window.TabSettings.IsVisible = true;
                window.TabRun.IsVisible = true;
                window.TabRun.IsSelected = true;
                window.ILauncherVersion.IsVisible = true;
                window.ILauncherVersion.Text = "App v" + MainWindow.LauncherVersion;
                window.SetupAllConfiguration();
                window.InstallProgress.Value = 100;
                window.InstallState.Text = "Done!!!";
            }
        };
    }

    private void GitPathBtnOnClick(object? sender, RoutedEventArgs e)
    {
        var tmp = LauncherConfig.Configuration;
        if (MainWindow.Instance.SGitPath.Text != null)
            LauncherConfig.Configuration.GitPath = MainWindow.Instance.SGitPath.Text;
        if (!File.Exists(LauncherConfig.Configuration.GitPath))
        {
            MainWindow.Instance.AdvancedError.Text += "Git does not exist.";
        }
        else
        {
            LauncherConfig.Configuration.GitPath = tmp.GitPath;
            if (MainWindow.Instance.AdvancedError.Text != null)
                MainWindow.Instance.AdvancedError.Text =
                MainWindow.Instance.AdvancedError.Text.Replace("Git does not exist.", "");
        }
    }

    private void NpmPathBtnOnClick(object? sender, RoutedEventArgs e)
    {
        var tmp = LauncherConfig.Configuration;
        if (MainWindow.Instance.SNpmPath.Text != null)
            LauncherConfig.Configuration.NpmPath = MainWindow.Instance.SNpmPath.Text;
        if (!File.Exists(LauncherConfig.Configuration.NpmPath))
        {
            MainWindow.Instance.AdvancedError.Text += "NPM does not exist.";
        }
        else
        {
            LauncherConfig.Configuration.NpmPath = tmp.NpmPath;
            if (MainWindow.Instance.AdvancedError.Text != null)
                MainWindow.Instance.AdvancedError.Text =
                    MainWindow.Instance.AdvancedError.Text.Replace("NPM does not exist.", "");
        }
    }

    private void NodePathBtnOnClick(object? sender, RoutedEventArgs e)
    {
        var tmp = LauncherConfig.Configuration;
        if (MainWindow.Instance.SNodePath.Text != null)
            LauncherConfig.Configuration.NodePath = MainWindow.Instance.SNodePath.Text;
        if (!File.Exists(LauncherConfig.Configuration.NpmPath))
        {
            MainWindow.Instance.AdvancedError.Text += "Node does not exist.";
        }
        else
        {
            LauncherConfig.Configuration.NodePath = tmp.NodePath;
            if (MainWindow.Instance.AdvancedError.Text != null)
                MainWindow.Instance.AdvancedError.Text =
                MainWindow.Instance.AdvancedError.Text.Replace("Node does not exist.", "");
        }
    }

    private bool MigrateEnsureExists(string folder)
    {
        return (!Directory.Exists(folder) || !File.Exists(folder + "\\freedeck\\package.json"));
    }

    private void DirectoryChangedEvent(object? sender, RoutedEventArgs e)
    {
        if (sender is Button)
        {
            MainWindow.InstallPath = MainWindow.Instance.InstallationDirectory.Text!;
            MainWindow.Instance.SadDirectory.Text = "Directory: " + MainWindow.Instance.InstallationDirectory.Text;
        }
    }

    private void ToggleReleaseEvent(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch)
        {
            MainWindow.Instance.SadRelease.Text = "Release Channel: " + ContentOf(MainWindow.Instance.SaRelease);
        }
    }

    private void ToggleWelcomeEvent(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch)
        {
            MainWindow.Instance.SadWelcome.Text = "Show Welcome Tiles: " + ContentOf(MainWindow.Instance.SaWelcome);
        }
    }
}