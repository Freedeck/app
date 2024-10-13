using System;
using System.Diagnostics;
using System.IO;
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
            string sourceExePath = Path.Combine(AppContext.BaseDirectory, executableName);
            File.Copy(sourceExePath, MainWindow.InstallPath + "\\Freedeck.exe", true);
        }
    }

    public void OnLaunch(MainWindow window)
    {
        window.Title = "Freedeck Installer";
        window.ILauncherVersion.IsVisible = false;
        window.TabRun.IsVisible = false;
        window.TabMarketplace.IsVisible = false;
        window.TabSettings.IsVisible = false;
        window.TabConfig.IsVisible = false;
        window.TabInstall.IsVisible = false;
        window.TabInstall.IsSelected = true;
        if (string.IsNullOrEmpty(MainWindow.InstallPath))
        {
            MainWindow.InstallPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Freedeck";
            LauncherConfig.Configuration.InstallationDirectory = MainWindow.InstallPath;
            LauncherConfig.Update();
        }
        window.InstallationDirectory.Text = MainWindow.InstallPath;
        window.SadDirectory.Text = "Directory:" +  window.InstallationDirectory.Text;
        
        window.SaAuthentication.IsCheckedChanged += ToggleAuthenticationEvent;
        window.SaWelcome.IsCheckedChanged += ToggleWelcomeEvent;
        window.SaRelease.IsCheckedChanged += ToggleReleaseEvent;
        window.InstallationDirectory.TextChanged += DirectoryChangedEvent;

        window.SaNext.Click += (sender, args) =>
        {
            window.InstallerNest.SelectedIndex++;
        };

        window.SaMigrate.Click += (sender, args) =>
        {
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
                if(IsChecked(MainWindow.Instance.SaSDesktop)) FreedeckAppInstaller.AppShortcutToDesktop("Freedeck", MainWindow.InstallPath +"\\Freedeck.exe");
                if(IsChecked(MainWindow.Instance.SaSStart)) FreedeckAppInstaller.AppShortcutToDesktop("Freedeck", MainWindow.InstallPath +"\\Freedeck.exe", Environment.SpecialFolder.StartMenu);
                window.InstallProgress.Value = 70;
                window.InstallState.Text = "Setting up configuration";
                LauncherConfig.Configuration.InstallationDirectory = window.InstallationDirectory.Text;
                LauncherConfig.Update();
                window.InstallProgress.Value = 90;
                window.InstallState.Text = "Done!! Fixing UI!";
                MainWindow.InstallPath = window.InstallationDirectory.Text;
                MainWindow.GetAndSetVersionData();
                window.Title = "Freedeck Launcher";
                window.TabInstall.IsVisible = false;
                window.TabMarketplace.IsVisible = true;
                window.TabConfig.IsVisible = true;
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

    private bool MigrateEnsureExists(string folder)
    {
        return (!Directory.Exists(folder) || !File.Exists(folder + "\\freedeck\\package.json"));
    }

    private void DirectoryChangedEvent(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox)
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