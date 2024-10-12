using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Freedeck
{
    public partial class Autoupdater : Window
    {
        public Autoupdater()
        {
            InitializeComponent();
            StartUpdateAsync();
        }

        private async void StartUpdateAsync()
        {
            AuProgress.Value = 10;

            string av = MainWindow.AppVersion;
            if (!File.Exists(MainWindow.InstallPath + "\\freedeck\\src\\server.js"))
            {
                AuProgress.Value = 0;
                AuCurrent.Text = "Installation path is invalid.";
                return;
            }

            string userConfig = File.ReadAllText(MainWindow.InstallPath + "\\freedeck\\src\\server.js");
            string branch = "v6";
            int line = 0;
            if (userConfig.Contains("release:\"dev\""))
            {
                branch = "v6-dev";
                line = 1;
            }

            try
            {
                HttpClient wc = new HttpClient();
                string res = await wc.GetStringAsync("https://freedeck.app/release");

                if (!av.Contains("6"))
                {
                    AuCurrent.Text = av + " doesn't seem like a valid version...";
                    AuProgress.Value = 0;
                    return;
                }

                string cv = res.Split("\n")[line];
                if (av != cv)
                {
                    AuProgress.Value = 50;
                    AuCurrent.Text = $"Updating! ({av} -> {cv}) on {branch}";
                    
                    // Run processes asynchronously
                    await RunProcessAsync("C:\\Program Files\\Git\\bin\\git.exe", $"checkout -f {branch}");
                    await RunProcessAsync("C:\\Program Files\\Git\\bin\\git.exe", "pull");
                    await RunProcessAsync("C:\\Program Files\\nodejs\\npm.cmd", "i");
                }
                else
                {
                    MainWindow.ShouldShowAutoUpdater = false;
                    this.Close();
                }

                AuProgress.Value = 100;
                AuCurrent.Text = "Up to date.";
                this.Close();
            }
            catch (Exception ex)
            {
                AuProgress.Value = 0;
                AuCurrent.Text = $"Error: {ex.Message}";
            }
        }

        private async Task RunProcessAsync(string file, string args)
        {
            AuProgress.Value = 0;
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
                    AuCurrent.Text = $"Error during process: {error}";
                }
            }
        }
    }
}
