using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Freedeck.Fakedeck;
using ShellLink;

namespace Freedeck;

public class FreedeckAppSchema
{
    public string? InternetUrl { get; init; }
    public string? PathToSaveTo { get; init; }
    public string? Expected { get; init; }
    public string? InitialMessage { get; init; }
    public string? Message { get; init; }
    public Func<Task>? Callback { get; init; }
}

public class FreedeckAppInstaller
{
    private readonly string folder = LauncherConfigSchema.AppData;
    private HttpClient? httpClient;

    public FreedeckAppInstaller()
    {
        httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10); // Set reasonable timeout
    }

    public async Task InstallAsync(Func<Task> callback)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.AppInstallLogContainer.IsVisible = true;
                MainWindow.Instance.InstallState.Text = "Creating folder...";
            });

            // Create shortcuts if requested
            await CreateShortcutsAsync();
            
            await UpdateProgress(15, "Starting downloads...");

            // Define installation schemas
            var nodeSchema = new FreedeckAppSchema
            {
                InternetUrl = "https://nodejs.org/dist/v20.15.0/node-v20.15.0-x64.msi",
                PathToSaveTo = Path.Combine(folder, "fd_node_install.msi"),
                Expected = @"C:\Program Files\nodejs\node.exe",
                InitialMessage = "Checking for Node.js...",
                Message = "Please follow the Node.js installer's instructions!",
                Callback = () => StageTwoAsync(callback)
            };

            var gitSchema = new FreedeckAppSchema
            {
                InternetUrl = "https://github.com/git-for-windows/git/releases/download/v2.47.0.windows.1/Git-2.47.0-64-bit.exe",
                PathToSaveTo = Path.Combine(folder, "fd_git_install.exe"),
                Expected = @"C:\Program Files\Git\bin\git.exe",
                InitialMessage = "Checking for Git...",
                Message = "Please follow the Git installer's instructions!",
                Callback = () => InstallDependencyAsync(nodeSchema)
            };

            await InstallDependencyAsync(gitSchema);
        }
        catch (Exception ex)
        {
            await HandleError($"Installation failed: {ex.Message}", ex);
        }
    }

    private async Task CreateShortcutsAsync()
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (SetupLogic.IsChecked(MainWindow.Instance.SaSDesktop))
                {
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    AppShortcutToDesktop("Freedeck", Path.Combine(LauncherConfigSchema.AppData, "Freedeck.exe"),
                        desktopPath);
                }

                if (SetupLogic.IsChecked(MainWindow.Instance.SaSStart))
                {
                    var startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        "Programs");
                    AppShortcutToDesktop("Freedeck", Path.Combine(LauncherConfigSchema.AppData, "Freedeck.exe"),
                        startMenuPath);
                }
            });

            LauncherConfig.ReloadConfiguration();
        }
        catch (Exception ex)
        {
            await LogMessage("Shortcut", InternalLogType.Err, $"Failed to create shortcuts: {ex.Message}");
        }
    }

    private async Task UpdateProgress(int value, string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.InstallProgress.Value = value;
            MainWindow.Instance.InstallState.Text = message;
        });
    }

    private async Task LogMessage(string title, InternalLogType logType, string logMessage)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var fmt = $"[{title}] {logMessage}";
            Console.WriteLine($"{fmt} ({logType})");
            MainWindow.Instance.AppInstallLog.Text += fmt + "\n";
        });
    }

    private async Task InstallDependencyAsync(FreedeckAppSchema schema)
    {
        try
        {
            await UpdateProgress(20, schema.InitialMessage!);

            // Check if already exists
            if (File.Exists(schema.Expected!) || File.Exists(schema.PathToSaveTo!))
            {
                await LogMessage("Install", InternalLogType.Out, $"Found existing installation: {schema.Expected}");
                await schema.Callback!();
                return;
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(schema.PathToSaveTo!)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Download the file
            await LogMessage("Download", InternalLogType.Out, $"Downloading from {schema.InternetUrl}");
            
            using var response = await httpClient!.GetAsync(schema.InternetUrl!);
            response.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(schema.PathToSaveTo!, FileMode.Create, FileAccess.Write);
            await response.Content.CopyToAsync(fileStream);

            await LogMessage("Download", InternalLogType.Out, "Download completed successfully");

            // Update UI and start installer
            await UpdateProgress(25, schema.Message!);

            // Start the installer process
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = schema.PathToSaveTo!,
                    WorkingDirectory = folder,
                    UseShellExecute = true // Allow UAC elevation
                },
                EnableRaisingEvents = true
            };

            var tcs = new TaskCompletionSource<bool>();
            
            process.Exited += async (sender, args) =>
            {
                await LogMessage("Install", InternalLogType.Out, $"Installer process exited with code: {process.ExitCode}");
                
                // Verify installation
                if (File.Exists(schema.Expected!))
                {
                    await LogMessage("Install", InternalLogType.Out, "Installation verified successfully");
                    await schema.Callback!();
                }
                else
                {
                    await HandleError($"Installation failed - expected file not found: {schema.Expected}", null);
                }
                
                tcs.SetResult(true);
            };

            process.Start();
            await tcs.Task; // Wait for process to complete
        }
        catch (Exception ex)
        {
            await HandleError($"Failed to install dependency: {ex.Message}", ex);
        }
    }

    private async Task StageTwoAsync(Func<Task> finish)
    {
        try
        {
            await UpdateProgress(30, "Fetching Git repository (github: freedeck/freedeck)");

            var gitPath = LauncherConfig.Configuration.GitPath;
            if (string.IsNullOrEmpty(gitPath) || !File.Exists(gitPath))
            {
                throw new InvalidOperationException("Git executable not found");
            }

            var targetPath = Path.Combine(MainWindow.InstallPath, "freedeck");
            
            // Remove existing directory if it exists
            if (Directory.Exists(targetPath))
            {
                Directory.Delete(targetPath, true);
                await LogMessage("Git", InternalLogType.Out, "Removed existing directory");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = gitPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            // Add arguments
            process.StartInfo.ArgumentList.Add("clone");
            process.StartInfo.ArgumentList.Add("https://github.com/freedeck/freedeck");
            process.StartInfo.ArgumentList.Add(targetPath);
            process.StartInfo.ArgumentList.Add("-b");
            process.StartInfo.ArgumentList.Add("v6" + (SetupLogic.IsChecked(MainWindow.Instance.SaRelease) ? "" : "-dev"));

            // Set up event handlers
            process.OutputDataReceived += async (sender, args) => 
            {
                if (!string.IsNullOrEmpty(args.Data))
                    await LogMessage("Git", InternalLogType.Out, args.Data);
            };
            
            process.ErrorDataReceived += async (sender, args) => 
            {
                if (!string.IsNullOrEmpty(args.Data))
                    await LogMessage("Git", InternalLogType.Err, args.Data);
            };

            var tcs = new TaskCompletionSource<bool>();
            
            process.Exited += async (sender, args) =>
            {
                if (process.ExitCode == 0)
                {
                    await LogMessage("Git", InternalLogType.Out, "Repository cloned successfully");
                    await StageThreeAsync(finish);
                }
                else
                {
                    await HandleError($"Git clone failed with exit code: {process.ExitCode}", null);
                }
                tcs.SetResult(true);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await tcs.Task;
        }
        catch (Exception ex)
        {
            await HandleError($"Git clone failed: {ex.Message}", ex);
        }
    }

    private async Task StageThreeAsync(Func<Task> finish)
    {
        try
        {
            await UpdateProgress(40, "Cloned Git repository! Installing dependencies...");

            var npmPath = LauncherConfig.Configuration.NpmPath;
            if (string.IsNullOrEmpty(npmPath) || !File.Exists(npmPath))
            {
                throw new InvalidOperationException("NPM executable not found");
            }

            var workingDirectory = Path.Combine(MainWindow.InstallPath, "freedeck");
            if (!Directory.Exists(workingDirectory))
            {
                throw new DirectoryNotFoundException($"Freedeck directory not found: {workingDirectory}");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = npmPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = workingDirectory
                },
                EnableRaisingEvents = true
            };

            process.StartInfo.ArgumentList.Add("install");

            // Set up event handlers
            process.OutputDataReceived += async (sender, args) => 
            {
                if (!string.IsNullOrEmpty(args.Data))
                    await LogMessage("NPM", InternalLogType.Out, args.Data);
            };
            
            process.ErrorDataReceived += async (sender, args) => 
            {
                if (!string.IsNullOrEmpty(args.Data))
                    await LogMessage("NPM", InternalLogType.Err, args.Data);
            };

            var tcs = new TaskCompletionSource<bool>();
            
            process.Exited += async (sender, args) =>
            {
                if (process.ExitCode == 0)
                {
                    await LogMessage("NPM", InternalLogType.Out, "Dependencies installed successfully");
                    await StageFourAsync(finish);
                }
                else
                {
                    await HandleError($"NPM install failed with exit code: {process.ExitCode}", null);
                }
                tcs.SetResult(true);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await tcs.Task;
        }
        catch (Exception ex)
        {
            await HandleError($"NPM install failed: {ex.Message}", ex);
        }
    }

    private async Task StageFourAsync(Func<Task> finish)
    {
        try
        {
            await UpdateProgress(85, "Making your configuration...");

            await Dispatcher.UIThread.InvokeAsync(FakeConfig.CreateDefaultConfiguration);
            
            await UpdateProgress(100, "Installation completed successfully!");
            await LogMessage("Install", InternalLogType.Out, "Installation process completed");
            
            // Small delay to let user see completion message
            await Task.Delay(1000);
            
            await Dispatcher.UIThread.InvokeAsync(finish);
        }
        catch (Exception ex)
        {
            await HandleError($"Configuration creation failed: {ex.Message}", ex);
        }
    }

    private async Task HandleError(string message, Exception? ex)
    {
        await LogMessage("Error", InternalLogType.Err, message);
        
        if (ex != null)
        {
            await LogMessage("Error", InternalLogType.Err, $"Exception details: {ex}");
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.InstallState.Text = "Installation failed. Check the log for details.";
        });
    }

    public static void AppShortcutToDesktop(string linkName, string app, string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var shortcutPath = Path.Combine(folder, linkName + ".lnk");
            
            // Remove existing shortcut
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            // Create new shortcut
            var shortcut = Shortcut.CreateShortcut(app, "", LauncherConfigSchema.AppData);
            shortcut.WriteToFile(shortcutPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create shortcut: {ex.Message}");
        }
    }

    public static void Initialize()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
            var appPath = Path.Combine(LauncherConfigSchema.AppData, "Freedeck.exe");

            // Update shortcuts if they exist (convert .url to .lnk)
            var startMenuShortcut = Path.Combine(startMenu, "Freedeck.url");
            if (File.Exists(startMenuShortcut))
            {
                File.Delete(startMenuShortcut);
                AppShortcutToDesktop("Freedeck", appPath, startMenu);
            }

            var desktopShortcut = Path.Combine(desktop, "Freedeck.url");
            if (File.Exists(desktopShortcut))
            {
                File.Delete(desktopShortcut);
                AppShortcutToDesktop("Freedeck", appPath, desktop);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialization failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        httpClient?.Dispose();
    }
}