﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Freedeck
{
    public enum AUState
    {
        INVALID_INSTALL_PATH,
        INVALID_APP_VERSION,
        NOFAIL_TEST_MODE,
        NOFAIL_COMPLETE,
        NOFAIL_OFFLINE,
        ERROR
    }

    public class AppReleaseNow
    {
        public string Now { get; set; } = "0.0.0";
        public string Latest { get; set; } = "0.0.0";
        public string? Error { get; set; } = "";
    }
    public partial class Autoupdater : Window
    {
        private static Autoupdater? instance;
        public static bool isInstanceCreated => instance != null;
        public static void Close() {(((Window)instance!)!).Close();}
        public Autoupdater()
        {
            InitializeComponent();
            instance = this;
        }

        public static async Task<AppReleaseNow> GetData()
        {
            string av = MainWindow.AppVersion;
            Console.WriteLine($"Freedeck is currently on version {av}");
            if (!File.Exists(MainWindow.InstallPath + "\\freedeck\\src\\configs\\config.fd.js"))
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainWindow.Instance.ProgressBarApp.Value = 0;
                    MainWindow.Instance.ProgressBarCurrently.Text = "Invalid app install path";
                });
                return new AppReleaseNow()
                {
                    Now = av,
                    Error = "Invalid app install path"
                };
            }
            if (!av.Contains("6"))
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainWindow.Instance.ProgressBarApp.Value = 10;
                    MainWindow.Instance.ProgressBarCurrently.Text = "Your app version seems to be invalid.";
                });
                return new AppReleaseNow()
                {
                    Now = av,
                    Error = "Invalid app version"
                };
            }
            
            ReleaseVersioningChannel channel =
                await ReleaseHelper.GetChannel(LauncherConfig.Configuration.InstallationInformation.SourceChannel);
            string cv = ReleaseHelper.GetLatestVersionFor(channel);
            return new AppReleaseNow()
            {
                Now = av,
                Latest = cv
            };
        }

        public static async Task<AUState> StartUpdateAsync()
        {
            if (LauncherConfig.Configuration.ShowAutoupdaterWindow)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Autoupdater au = new Autoupdater();
                    au.Show();
                });
            }
            await Dispatcher.UIThread.InvokeAsync(MainWindow.GetAndSetVersionData);
            AppReleaseNow currentlyRelease = await GetData();

            ReleaseVersioningChannel channel =
                await ReleaseHelper.GetChannel(LauncherConfig.Configuration.InstallationInformation.SourceChannel);
            string branch = channel.branch;
            if(channel.error == true) return AUState.ERROR;
            
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.ProgressBarApp.Value = 20;
                MainWindow.Instance.ProgressBarCurrently.Text = $"Using branch {branch}";
            });

            try
            {
                string av = currentlyRelease.Now;
                string cv = currentlyRelease.Latest;
                if (av != cv)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 50;
                        MainWindow.Instance.ProgressBarCurrently.Text = $"Updating! ({av} -> {cv}) on {branch}";
                    });

                    if (MainWindow.AutoUpdaterTestMode) return AUState.NOFAIL_TEST_MODE;
                    if (!ReleaseHelper.IsOnline)
                    {
                        StdoutLog("Freedeck Autoupdater", InternalLogType.Out, "You are offline.");
                        return AUState.NOFAIL_OFFLINE;
                    }
                    // Run processes asynchronously
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 60;
                        MainWindow.Instance.ProgressBarCurrently.Text = $"Checking out to {branch}";
                    });
                    await RunProcessAsync(LauncherConfig.Configuration.GitPath, $"checkout -f {branch}", "Git Checkout");
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 65;
                        MainWindow.Instance.ProgressBarCurrently.Text = $"Pulling {cv} from {branch}";
                    });
                    await RunProcessAsync(LauncherConfig.Configuration.GitPath, "pull", "Git Pull");
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 80;
                        MainWindow.Instance.ProgressBarCurrently.Text = "Reinstalling dependencies...";
                    });
                    await RunProcessAsync(LauncherConfig.Configuration.NpmPath, "i", "NPM");
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 100;
                        MainWindow.Instance.ProgressBarCurrently.Text = "Done!";
                        StdoutLog("Freedeck Autoupdater", InternalLogType.Out, "Update complete! Went from " + av + " to " + cv);
                        StdoutLog("Freedeck Autoupdater", InternalLogType.Out, "Starting Freedeck. You may close this window.");

                        _ = ReleaseHelper.FullyUpdate();
                    });
                }
                else
                {
                    StdoutLog("Freedeck Autoupdater", InternalLogType.Out, "Nothing to update!");
                    StdoutLog("Freedeck Autoupdater", InternalLogType.Out, "Starting Freedeck. You may close this window.");
                }

                return AUState.NOFAIL_COMPLETE;
            }
            catch (Exception ex)
            {
                return AUState.ERROR;
            }
        }

        private static void StdoutLog(string title, InternalLogType logType, string logMessage)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                String fmt = $"{title} {(logType == InternalLogType.Err ? "(err)" : "")} >> {logMessage}";
                Console.WriteLine(fmt);
                if(instance != null) instance.AutoupdaterState.Text += fmt + "\n";
            });
        }

        private static async Task RunProcessAsync(string file, string args, string title)
        {
            using (Process proc = new Process())
            {
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                proc.StartInfo.WorkingDirectory = MainWindow.InstallPath + "\\freedeck";
                proc.StartInfo.FileName = file;
                proc.StartInfo.Arguments = " " + args;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.UseShellExecute = false;
                proc.OutputDataReceived += (sender, args) => StdoutLog(title, InternalLogType.Out, args.Data!);
                proc.ErrorDataReceived += (sender, args) => StdoutLog(title, InternalLogType.Err, args.Data!);
                StdoutLog("Freedeck Autoupdater", InternalLogType.Out, $"Running {file} {args}");
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                await proc.WaitForExitAsync();
            }
        }
    }
}
