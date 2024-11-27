using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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
    public static string LauncherVersion = "Beta 3";
    public static string BuildId = "d7801948b7e477b1c467e1778992bb2cdbda0597";
    public static bool ShouldShowAutoUpdater = true;
    public static bool AutoUpdaterTestMode = false;
    private bool _isUndergoingModification = false;
    public static MainWindow Instance = null!;
    private static readonly SetupLogic SetupLogic = new SetupLogic();

    private static string CreateBuildId()
    {
        //TODO: Implemetn (lol)
        return "fd7801948b7e477b1c467e1778992bb2cdbda0597";
    }

    private void InstallerDoInstall(object? sender, RoutedEventArgs e)
    {
        if (InstallationDirectory.Text != null) InstallPath = InstallationDirectory.Text;
        LauncherConfig.Configuration.InstallationDirectory = InstallPath;
        LauncherConfig.Update();
        FreedeckAppInstaller inst = new FreedeckAppInstaller();
        ITabSetup.IsVisible = false;
        ITabInstall.Header = "Installing...";
        InstallerBtn.IsVisible = false;

        ITabInstallTxt.Text = "Installing Freedeck...";
        ITabInstallDesc.Text =
            "";

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
        TabSettings.IsVisible = true;
        TabRun.IsVisible = true;
        TabRun.IsSelected = true;
        ILauncherVersion.IsVisible = true;
        ILauncherVersion.Text = "App " + LauncherVersion;
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
        Instance.ILauncherVersion.Text = "App " + LauncherVersion;
        Instance.SFreedeckPath.Text = InstallPath;
    }

    private bool IsLauncherInstalled()
    {
        return File.Exists(LauncherConfigSchema.AppData + "\\Freedeck.exe");
    }
    
    private bool IsAppInstalled()
    {
        return File.Exists(LauncherConfig.Configuration.InstallationDirectory + "\\freedeck\\package.json");
    }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        BuildIdUser.Text = "FDApp Build Identifier: " + BuildId;
        LauncherConfig.ReloadConfiguration();
        HandoffHelper.Initialize();
        LauncherPersonalization.Initialize();
        NativeBridge.Initialize();
        _ = Task.Run(async () =>
        {
            await ReleaseHelper.FullyUpdate();
        });
        NewBuildId.Click += (sender, args) =>
        {
            using SHA1 shaM = SHA1.Create();
            DateTime dt = DateTime.Now;
            string dstr = dt.ToLongDateString();
            string tstr = DateTime.Now.ToLongTimeString();
            byte[] rawHash = shaM.ComputeHash(Encoding.UTF8.GetBytes(dstr + tstr));
            string hash = "";
            foreach (byte x in rawHash)
            {
                hash += $"{x:x2}";
            }

            BuildIdBoxBeta.Text = hash;
            rawHash = shaM.ComputeHash(Encoding.UTF8.GetBytes(dstr));
            hash = "";
            foreach (byte x in rawHash)
            {
                hash += $"{x:x2}";
            }
            BuildIdBox.Text = hash;
        };
        if (IsAppInstalled())
        {
            GetAndSetVersionData();
            TabInstall.IsVisible = false;
            TabRun.IsSelected = true;
            SetupAllConfiguration();
        }
        else
            SetupLogic.OnLaunch(this);
    }

    public void SetupAllConfiguration()
    {
        LauncherConfig.ReloadConfiguration();
        _isUndergoingModification = true;
        SFreedeckPath.Text = LauncherConfig.Configuration.InstallationDirectory;
        SLCPath.Text = LauncherConfig.Configuration.ConfigurationPath;
        SLCServer.Text = LauncherConfig.Configuration.ServerUrl;
        SLCRelease.Text = ReleaseHelper.server + "/" + ReleaseHelper.file;
        SLCNode.Text = LauncherConfig.Configuration.NodePath;
        SLCNpm.Text = LauncherConfig.Configuration.NpmPath;
        SLCGit.Text = LauncherConfig.Configuration.GitPath;
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

        _isUndergoingModification = false;
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if(_isUndergoingModification) return;
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
            await Autoupdater.StartUpdateAsync();
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
    

    private void ToggleShowTerminal(object? sender, RoutedEventArgs e)
    {
        if(_isUndergoingModification) return;
        LauncherConfig.Configuration.ShowTerminal = !LauncherConfig.Configuration.ShowTerminal;
        ShowTerminal.IsChecked = LauncherConfig.Configuration.ShowTerminal;
        LauncherConfig.Update();
    }

    private void AutoUpdateMode_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if(_isUndergoingModification) return;
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
            InstallationDirectory = "",
            ShowTerminal = false,
            AutoUpdaterBehavior = 0,
            ConfigurationPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FreedeckLauncherConfiguration.json"
        };
        LauncherConfig.Update();
        string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath != null) Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        Environment.Exit(0);
    }

    private void CopyToLoc(object? sender, RoutedEventArgs e)
    {
        SetupLogic.CopyLauncherToInstallation();
    }

    private void OpenAuTest(object? sender, RoutedEventArgs e)
    {
        AutoUpdaterTestMode = true;
        // Autoupdater au = new Autoupdater();
        // au.Show();
        LeftSidebar.IsVisible = false;
        TabRun.IsVisible = false;
        TabSettings.IsVisible = false;
    }
}