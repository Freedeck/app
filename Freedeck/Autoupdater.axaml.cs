using System;
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
        ERROR
    }
    public partial class Autoupdater : Window
    {
        public Autoupdater()
        {
            InitializeComponent();
        }

        public static async Task<AUState> StartUpdateAsync()
        {
            string av = MainWindow.AppVersion;
            if (!File.Exists(MainWindow.InstallPath + "\\freedeck\\src\\configs\\config.fd.js"))
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainWindow.Instance.ProgressBarApp.Value = 0;
                    MainWindow.Instance.ProgressBarCurrently.Text = "Invalid app install path";
                });
                return AUState.INVALID_INSTALL_PATH;
            }

            string userConfig = await File.ReadAllTextAsync(MainWindow.InstallPath + "\\freedeck\\src\\configs\\config.fd.js");
            string branch = "v6";
            int line = 0;
            if (!av.Contains("6"))
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainWindow.Instance.ProgressBarApp.Value = 10;
                    MainWindow.Instance.ProgressBarCurrently.Text = "Your app version seems to be invalid.";
                });
                return AUState.INVALID_APP_VERSION;
            }
            if (userConfig.Contains("\"release\":\"dev\"") || userConfig.Contains("release:\"dev\""))
            {
                branch = "v6-dev";
                line = 1;
            }
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.ProgressBarApp.Value = 20;
                MainWindow.Instance.ProgressBarCurrently.Text = $"Using branch {branch}";
            });

            try
            {
                HttpClient wc = new HttpClient();
                var get = await wc.GetAsync("https://freedeck.app/release");
                if (get.StatusCode != HttpStatusCode.Accepted)
                {
                    ReleaseHelper.isOnline = false;
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 0;
                        MainWindow.Instance.ProgressBarCurrently.Text = "You are offline! Failed to fetch server release.";
                    });
                } else ReleaseHelper.isOnline = true;
                string res = await get.Content.ReadAsStringAsync();
                string cv = res.Split("\n")[line];
                if (av != cv)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 50;
                        MainWindow.Instance.ProgressBarCurrently.Text = $"Updating! ({av} -> {cv}) on {branch}";
                    });

                    if (MainWindow.AutoUpdaterTestMode) return AUState.NOFAIL_TEST_MODE;
                    // Run processes asynchronously
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 60;
                        MainWindow.Instance.ProgressBarCurrently.Text = $"Checking out to {branch}";
                    });
                    await RunProcessAsync("C:\\Program Files\\Git\\bin\\git.exe", $"checkout -f {branch}");
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 65;
                        MainWindow.Instance.ProgressBarCurrently.Text = $"Pulling {cv} from {branch}";
                    });
                    await RunProcessAsync("C:\\Program Files\\Git\\bin\\git.exe", "pull");
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 80;
                        MainWindow.Instance.ProgressBarCurrently.Text = "Reinstalling dependencies...";
                    });
                    await RunProcessAsync("C:\\Program Files\\nodejs\\npm.cmd", "i");
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        MainWindow.Instance.ProgressBarApp.Value = 100;
                        MainWindow.Instance.ProgressBarCurrently.Text = "Done!";
                    });
                }
                else
                {
                    MainWindow.ShouldShowAutoUpdater = false;
                }

                return AUState.NOFAIL_COMPLETE;
            }
            catch (Exception ex)
            {
                return AUState.ERROR;
            }
        }


        private static async Task RunProcessAsync(string file, string args)
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

                proc.Start();

                // Read the process output asynchronously to avoid blocking
                string output = await proc.StandardOutput.ReadToEndAsync();
                string error = await proc.StandardError.ReadToEndAsync();

                await proc.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    await Console.Error.WriteLineAsync($"Error during process: {error}");
                }
            }
        }
    }
}
