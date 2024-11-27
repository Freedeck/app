using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Freedeck;

public class LauncherConfig
{
    public static LauncherConfigSchema Configuration = new LauncherConfigSchema
    {
        PersistentSettings = false,
        ShowConfigureTab = false,
        ShowMarketplaceTab = false,
        InstallationDirectory = MainWindow.InstallPath,
        ShowTerminal = false,
        AutoUpdaterBehavior = 0,
        ConfigurationPath = LauncherConfigSchema.AppData + "\\Launcher.json",
        LastReleaseFeedPrefix = LauncherConfigSchema.AppData + "\\CachedRelease\\feed-",
        LastReleaseIndex = LauncherConfigSchema.AppData + "\\CachedRelease\\index.json",
        NodePath = "C:\\Program Files\\nodejs\\node.exe",
        NpmPath = "C:\\Program Files\\nodejs\\npm.cmd",
        GitPath  = "C:\\Program Files\\Git\\bin\\git.exe",
        ServerUrl = "http://localhost:5754"
    };
    
    public static void ReloadConfiguration()
    {
        if (!File.Exists(Configuration.ConfigurationPath))
        {
            Update();
        }

        string json = File.ReadAllText(Configuration.ConfigurationPath);
        if (!string.IsNullOrEmpty(json))
        {
            var deserializedConfig = JsonSerializer.Deserialize<LauncherConfigSchema>(json);
            if (deserializedConfig != null)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!Directory.Exists(LauncherConfigSchema.AppData)) Directory.CreateDirectory(LauncherConfigSchema.AppData);
                if (File.Exists(appData + "\\FreedeckLauncherConfiguration.json"))
                {
                    File.WriteAllText(Configuration.ConfigurationPath, 
                        File.ReadAllText(appData + "\\FreedeckLauncherConfiguration.json")
                    );
                    File.Delete(appData + "\\FreedeckLauncherConfiguration.json");
                }
                
                MainWindow.InstallPath = Configuration.InstallationDirectory;
                File.WriteAllTextAsync(Configuration.ConfigurationPath, JsonSerializer.Serialize<LauncherConfigSchema>(Configuration));
            }
        }
    }

    public static void Update()
    {
        if (!Directory.Exists(LauncherConfigSchema.AppData)) Directory.CreateDirectory(LauncherConfigSchema.AppData);
        if (!Directory.Exists(LauncherConfigSchema.AppData + "\\CachedRelease")) Directory.CreateDirectory(LauncherConfigSchema.AppData + "\\CachedRelease");
        File.WriteAllText(Configuration.ConfigurationPath, JsonSerializer.Serialize<LauncherConfigSchema>(Configuration));
    }
}