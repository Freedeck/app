using System;

namespace Freedeck;

public class LauncherConfigSchema
{
    public bool PersistentSettings { get; set; }
    public bool ShowConfigureTab { get; set; }
    public bool ShowMarketplaceTab { get; set; }
    public string InstallationDirectory { get; set; } = MainWindow.InstallPath;
    public bool ShowTerminal { get; set; }
    public int AutoUpdaterBehavior { get; set; }
    public string ConfigurationPath { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FreedeckLauncherConfiguration.json";
    public string ServerUrl { get; set; } = "http://localhost:5754";
}