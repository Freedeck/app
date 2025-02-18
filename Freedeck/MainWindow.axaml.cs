using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
    public static string LauncherVersion = "1.0.0-rc6";
    public static string BuildId = "8c8f78b2f8969c60f627c1637b3797c40341f894";
    public static bool AutoUpdaterTestMode = false;
    private bool _isUndergoingModification = false;
    public static MainWindow Instance = null!;
    private static readonly SetupLogic SetupLogic = new SetupLogic();

    public static bool CloseHandler(bool force = false)
    {
        if (HandoffHelper.ActiveQuery)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Instance.Hide();
            });
            return true;
        };
        if (FreedeckAppRunner.ReallyCheckIfAppIsRunning() || force)
        {
           Dispatcher.UIThread.InvokeAsync(() =>
           {
                Instance.TabClose.IsVisible = true;
                Instance.TabClose.IsSelected = true;
                Instance.TabRun.IsVisible = false;
                Instance.TabSettings.IsVisible = false;
                bool[] list = FreedeckAppRunner.ReallyCheckIfAppIsRunningList();
                Instance.CloseAppForRealsiesText.Text = (list[0] ? (list[0]?"Node" + (list[1]?" and Electron are ":" is"):"Electron is") : "No essential processes are" )+ " still running.";
           });
            return true;
        }
        else
        {
            return false;
        }
    }
    
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        bool shouldDo = CloseHandler();
        if (shouldDo)
        {
            e.Cancel = true;
        }
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
        SaMigrate.IsVisible = false;

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
            ReleaseHelper.UpdateCatalogAsync();
        });
        TabInstall.IsVisible = false;
        TabSettings.IsVisible = true;
        TabRun.IsVisible = true;
        TabRun.IsSelected = true;
        ILauncherVersion.IsVisible = true;
        ILauncherVersion.Text = "App " + LauncherVersion;
        Instance.Title = "Freedeck";
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
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Instance.InstalledVersion.Text = "Companion v" + AppVersion;
            Instance.ILauncherVersion.Text = "App v" + LauncherVersion;
            Instance.SFreedeckPath.Text = InstallPath;
        });
    }

    private bool IsLauncherInstalled()
    {
        return File.Exists(LauncherConfigSchema.AppData + "\\Freedeck.exe");
    }
    
    private bool IsAppInstalled()
    {
        Console.WriteLine(InstallPath + "\\freedeck\\package.json");
        if(!Directory.Exists(InstallPath + "\\freedeck")) return false;
        return File.Exists(InstallPath + "\\freedeck\\package.json");
    }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        BuildIdUser.Text = "FDApp Build Identifier: " + BuildId;
        LauncherConfig.ReloadConfiguration();
        _ = Task.Run(async () =>
        { 
            LauncherConfig.UpdateLauncher();
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Application.Current?.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime { Args.Length: > 0 } desktop) HandoffHelper.HandleCommand(desktop.Args[0]);
                HandoffHelper.Initialize();
                LauncherPersonalization.Initialize();
                ReleaseHelper.FullyUpdate();
                FreedeckAppRunner.ProbeAndAttachRunningInstancesOfFreedeck();
            });
            NativeBridge.Initialize();
        });
        if (IsAppInstalled())
        {
            GetAndSetVersionData();
            Dispatcher.UIThread.InvokeAsync(SetupAllConfiguration);
        }
        else
            Dispatcher.UIThread.InvokeAsync(() => SetupLogic.OnLaunch(this));
    }

    public void SetupAllConfiguration()
    {
        LauncherConfig.ReloadConfiguration();
        _isUndergoingModification = true;
        TabInstall.IsVisible = false;
        TabClose.IsVisible = false;
        TabClose.IsSelected = false;
        // TabRun.IsSelected = true;
        SFreedeckPath.Text = LauncherConfig.Configuration.InstallationDirectory;
        SlcPath.Text = LauncherConfig.Configuration.ConfigurationPath;
        SlcServer.Text = LauncherConfig.Configuration.ServerUrl;
        Slcnbws.Text = "http://localhost:5756";
        SlcRelease.Text = ReleaseHelper.Server + "/" + ReleaseHelper.File;
        SLCNode.Text = LauncherConfig.Configuration.NodePath;
        SLCNpm.Text = LauncherConfig.Configuration.NpmPath;
        SLCGit.Text = LauncherConfig.Configuration.GitPath;
        ShowTerminal.IsChecked = LauncherConfig.Configuration.ShowTerminal;
        ShowAutoupdateWindow.IsChecked = LauncherConfig.Configuration.ShowAutoupdaterWindow;
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
        
        foreach (ComboBoxItem item in SChannelSelector.Items)
        {
            if(item.Tag.ToString() == LauncherConfig.Configuration.InstallationInformation.SourceChannel)
                SChannelSelector.SelectedItem = item;
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
            ReleaseHelper.FullyUpdate();
        });
    }

    private async void LaunchFreedeck(AppLaunchType launchType)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
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
            }

            ProgressBarCurrently.Text = "Sending state to FDAppRunner";
            ProgressBarApp.Value = 20;
            if (AutoUpdateMode.SelectedIndex == 1)
            {
                AutoUpdateMode.SelectedIndex = 0;
            }
        });
        _ = Task.Run(async () =>
        {
            AUState autoUpdater = await Autoupdater.StartUpdateAsync();
            Console.WriteLine("Autoupdater exited with code " + autoUpdater);
            FreedeckAppRunner.StartFreedeck(launchType);
        });
    }

    private void LaunchApp_OnClick(object? sender, RoutedEventArgs e)
    {
        LaunchFreedeck(AppLaunchType.All);
    }
    
    private void LaunchAppCompanion_OnClick(object? sender, RoutedEventArgs e)
    {
        LaunchFreedeck(AppLaunchType.Companion);
    }

    private void LaunchAppServer_OnClick(object? sender, RoutedEventArgs e)
    {
        LaunchFreedeck(AppLaunchType.Server);
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

    private void MkShortcuts(object? sender, RoutedEventArgs e)
    {
        FreedeckAppInstaller.AppShortcutToDesktop("Freedeck", LauncherConfigSchema.AppData + "\\Freedeck.exe", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        FreedeckAppInstaller.AppShortcutToDesktop("Freedeck", LauncherConfigSchema.AppData + "\\Freedeck.exe", Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + "\\Programs");              
    }

    private void ToggleShowAutoupdate(object? sender, RoutedEventArgs e)
    {
        if(_isUndergoingModification) return;
        LauncherConfig.Configuration.ShowAutoupdaterWindow = !LauncherConfig.Configuration.ShowAutoupdaterWindow;
        ShowAutoupdateWindow.IsChecked = LauncherConfig.Configuration.ShowAutoupdaterWindow;
        LauncherConfig.Update();
    }

    private void SChannelSelector_OnClick(object? sender, RoutedEventArgs e)
    {
        if(_isUndergoingModification) return;
        ComboBoxItem? item = (ComboBoxItem) SChannelSelector.SelectedItem;
        SChannelSelector.SelectedItem = item;
        LauncherConfig.Configuration.InstallationInformation.SourceChannel = item.Tag.ToString();
        LauncherConfig.Update();
        _ = Task.Run(async () => ReleaseHelper.FullyUpdate());
    }

    private void NewBuildId_OnClick(object? sender, RoutedEventArgs e)
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
    }

    private void CloseAppForRealsies(object? sender, RoutedEventArgs e)
    {
        FreedeckAppRunner.KillAllProcesses();
        TabClose.IsVisible = false;
        TabRun.IsVisible = true;
        TabSettings.IsVisible = true;
        Instance.Close(true);
    }

    private void Wrapper_CloseHandler(object? sender, RoutedEventArgs e)
    {
        CloseHandler(true);
    }
}