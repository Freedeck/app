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
        ConfigurationPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FreedeckLauncherConfiguration.json"
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
                Configuration = deserializedConfig; // Update Configuration only if deserialization succeeds
                MainWindow.InstallPath = Configuration.InstallationDirectory;
            }
        }
    }

    public static void Update()
    {
        File.WriteAllText(Configuration.ConfigurationPath, JsonSerializer.Serialize(Configuration));
    }
}