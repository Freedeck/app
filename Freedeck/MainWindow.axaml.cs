using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Freedeck.Fakedeck;

namespace Freedeck;

public partial class MainWindow : Window
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public static string InstallPath = Home + "\\Freedeck";
    public static string AppVersion = "1.0.0";
    public static string LauncherVersion = "2.0.0";
    public static bool ShouldShowAutoUpdater = true;
    private bool IsUndergoingModification = false;
    public static MainWindow Instance;
    private static SetupLogic _setupLogic = new SetupLogic();

    private void InstallerDoInstall(object? sender, RoutedEventArgs e)
    {
        MainWindow.InstallPath = InstallationDirectory.Text;
        LauncherConfig.Configuration.InstallationDirectory = MainWindow.InstallPath;
        LauncherConfig.Update();
        FreedeckAppInstaller inst = new FreedeckAppInstaller();
        ITabSetup.IsVisible = false;
        ITabInstall.Header = "Installing...";
        InstallerBtn.IsVisible = false;

        ITabInstallTxt.Text = "Installing Freedeck...";
        ITabInstallDesc.Text =
            "You may see some Command Prompt (cmd.exe) windows pop up! They're just Freedeck installing required components.";

        InstallState.Text = "Initializing installer helper...";
        InstallProgress.Value = 10;
        inst.InstallIt(InstallerFinishedHelper);
    }

    private void InstallerFinished()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            GetAndSetVersionData();
        });
        TabInstall.IsVisible = false;
        TabMarketplace.IsVisible = true;
        TabConfig.IsVisible = true;
        TabSettings.IsVisible = true;
        TabRun.IsVisible = true;
        TabRun.IsSelected = true;
        ILauncherVersion.IsVisible = true;
        ILauncherVersion.Text = "Launcher v" + LauncherVersion;
        SetupAllConfiguration();
    }

    private void InstallerFinishedHelper()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            InstallerFinished();
        });
    }

    public static void GetAndSetVersionData()
    {
        if (!Instance.IsAppInstalled())
        {
            Instance.InstalledVersion.Text = "Invalid directory";
            Instance.ILauncherVersion.Text = "Unable to find Freedeck.";
            return;
        }

        string uver = File.ReadAllText(InstallPath + "\\freedeck\\package.json");
        string version = uver.Split(new string[] { "\"version\": \"" }, StringSplitOptions.None)[1].Split('"')[0];
        AppVersion = version;
        Instance.InstalledVersion.Text = "Freedeck v" + AppVersion;
        Instance.ILauncherVersion.Text = "Launcher v" + LauncherVersion;
        Instance.SFreedeckPath.Text = InstallPath;
    }

    private bool IsAppInstalled()
    {
        return File.Exists(InstallPath + "\\freedeck\\package.json") ||
               File.Exists(LauncherConfig.Configuration.InstallationDirectory + "\\freedeck\\package.json");
    }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        ProgressBarContainer.IsVisible = false;
        LauncherConfig.ReloadConfiguration();
        HandoffHelper.Initialize();
        if (IsAppInstalled())
        {
            GetAndSetVersionData();
            TabInstall.IsVisible = false;
            TabConfig.IsVisible = true;
            TabRun.IsSelected = true;
            SetupAllConfiguration();
        }
        else
            _setupLogic.OnLaunch(this);
    }

    public void SetupAllConfiguration()
    {
        LauncherConfig.ReloadConfiguration();
        IsUndergoingModification = true;
        SConfigTab.IsChecked = LauncherConfig.Configuration.ShowConfigureTab;
        SMarketTab.IsChecked = LauncherConfig.Configuration.ShowMarketplaceTab;
        SFreedeckPath.Text = LauncherConfig.Configuration.InstallationDirectory;
        SLCPath.Text = LauncherConfig.Configuration.ConfigurationPath;
        SLCServer.Text = LauncherConfig.Configuration.ServerUrl;
        ShowTerminal.IsChecked = LauncherConfig.Configuration.ShowTerminal;
        AutoUpdateMode.SelectedIndex = LauncherConfig.Configuration.AutoUpdaterBehavior;
        if (AutoUpdateMode.SelectedIndex != 0)
        {
            UpdateCheckNotice.IsVisible = true;
            UpdateCheckNotice.Text =
                AutoUpdateMode.SelectedIndex == 1 ? "Freedeck will not check for updates on this run." : "Freedeck will not check for updates.";
        }
        else
        {
            UpdateCheckNotice.IsVisible = false;
        }

        TabConfig.IsVisible = LauncherConfig.Configuration.ShowConfigureTab;
        TabMarketplace.IsVisible = LauncherConfig.Configuration.ShowMarketplaceTab;
        IsUndergoingModification = false;
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if(IsUndergoingModification) return;
        if (SFreedeckPath.Text != null) InstallPath = SFreedeckPath.Text;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            GetAndSetVersionData();
        });
    }

    private async void LaunchApp_OnClick(object? sender, RoutedEventArgs e)
    {
        LaunchApp.IsEnabled = false;
        ProgressBarCurrently.Text = "Launching...";
        ProgressBarApp.Value = 1;
        ProgressBarContainer.IsVisible = true;
        if (!IsAppInstalled())
        {
            ProgressBarCurrently.Text = "Installation path invalid.";
            ProgressBarApp.Value = 0;
            LaunchApp.IsEnabled = true;
            return;
        }

        if (AutoUpdateMode.SelectedIndex == 0)
        {
            ProgressBarCurrently.Text = "Checking for updates...";
            ProgressBarApp.Value = 10;
            Autoupdater au = new Autoupdater();
            if (ShouldShowAutoUpdater) await au.ShowDialog(this);
        }

        ProgressBarCurrently.Text = "Sending state to FDAppRunner";
        ProgressBarApp.Value = 20;
        FreedeckAppRunner runner = new FreedeckAppRunner();
        runner.StartFreedeck(AppLaunchType.All);
        if (AutoUpdateMode.SelectedIndex == 1)
        {
            AutoUpdateMode.SelectedIndex = 0;
        }
    }

    private void ToggleConfigure(object? sender, RoutedEventArgs e)
    {
        if(IsUndergoingModification) return;
        LauncherConfig.Configuration.ShowConfigureTab = !LauncherConfig.Configuration.ShowConfigureTab;
        TabConfig.IsVisible = LauncherConfig.Configuration.ShowConfigureTab;
        LauncherConfig.Update();
    }

    private void ToggleButton_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if(IsUndergoingModification) return;
        LauncherConfig.Configuration.ShowMarketplaceTab = !LauncherConfig.Configuration.ShowMarketplaceTab;
        TabMarketplace.IsVisible = LauncherConfig.Configuration.ShowMarketplaceTab;
        LauncherConfig.Update();
    }

    private void ToggleShowTerminal(object? sender, RoutedEventArgs e)
    {
        if(IsUndergoingModification) return;
        LauncherConfig.Configuration.ShowTerminal = !LauncherConfig.Configuration.ShowTerminal;
        ShowTerminal.IsChecked = LauncherConfig.Configuration.ShowTerminal;
        LauncherConfig.Update();
    }

    private void AutoUpdateMode_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if(IsUndergoingModification) return;
        if (AutoUpdateMode.SelectedIndex != 0)
        {
            UpdateCheckNotice.IsVisible = true;
            UpdateCheckNotice.Text =
                AutoUpdateMode.SelectedIndex == 1 ? "Freedeck will not check for updates on this run." : "Freedeck will not check for updates.";
        }
        else
        {
            UpdateCheckNotice.IsVisible = false;
        }
        LauncherConfig.Configuration.AutoUpdaterBehavior = AutoUpdateMode.SelectedIndex;
        LauncherConfig.Update();
    }

    private async void LaunchAppCompanion_OnClick(object? sender, RoutedEventArgs e)
    {
        LaunchApp.IsEnabled = false;
        ProgressBarCurrently.Text = "Launching...";
        ProgressBarApp.Value = 1;
        ProgressBarContainer.IsVisible = true;
        if (!IsAppInstalled())
        {
            ProgressBarCurrently.Text = "Installation path invalid.";
            ProgressBarApp.Value = 0;
            LaunchApp.IsEnabled = true;
            return;
        }

        if (AutoUpdateMode.SelectedIndex == 0)
        {
            ProgressBarCurrently.Text = "Checking for updates...";
            ProgressBarApp.Value = 10;
            Autoupdater au = new Autoupdater();
            if (ShouldShowAutoUpdater) await au.ShowDialog(this);
        }

        ProgressBarCurrently.Text = "Sending state to FDAppRunner";
        ProgressBarApp.Value = 20;
        FreedeckAppRunner runner = new FreedeckAppRunner();
        runner.StartFreedeck(AppLaunchType.Companion);
        if (AutoUpdateMode.SelectedIndex == 1)
        {
            AutoUpdateMode.SelectedIndex = 0;
        }
    }

    private async void LaunchAppServer_OnClick(object? sender, RoutedEventArgs e)
    {
        LaunchApp.IsEnabled = false;
        ProgressBarCurrently.Text = "Launching...";
        ProgressBarApp.Value = 1;
        ProgressBarContainer.IsVisible = true;
        if (!IsAppInstalled())
        {
            ProgressBarCurrently.Text = "Installation path invalid.";
            ProgressBarApp.Value = 0;
            LaunchApp.IsEnabled = true;
            return;
        }

        if (AutoUpdateMode.SelectedIndex == 0)
        {
            ProgressBarCurrently.Text = "Checking for updates...";
            ProgressBarApp.Value = 10;
            Autoupdater au = new Autoupdater();
            if (ShouldShowAutoUpdater) await au.ShowDialog(this);
        }

        ProgressBarCurrently.Text = "Sending state to FDAppRunner";
        ProgressBarApp.Value = 20;
        FreedeckAppRunner runner = new FreedeckAppRunner();
        runner.StartFreedeck(AppLaunchType.Server);
        if (AutoUpdateMode.SelectedIndex == 1)
        {
            AutoUpdateMode.SelectedIndex = 0;
        }
    }

    private void Reset_Configuration(object? sender, RoutedEventArgs e)
    {
        LauncherConfig.Configuration = new LauncherConfigSchema
        {
            PersistentSettings = false,
            ShowConfigureTab = false,
            ShowMarketplaceTab = false,
            InstallationDirectory = "",
            ShowTerminal = false,
            AutoUpdaterBehavior = 0,
            ConfigurationPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FreedeckLauncherConfiguration.json"
        };
        LauncherConfig.Update();
        string exePath = Process.GetCurrentProcess().MainModule.FileName; 
        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true }); 
        Environment.Exit(0);
    }

    private void CopyToLoc(object? sender, RoutedEventArgs e)
    {
        SetupLogic.CopyLauncherToInstallation();
    }
}