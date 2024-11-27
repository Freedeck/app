using System;

namespace Freedeck;

public class LauncherConfigSchema
{
    public static string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FreedeckApp";
    public bool PersistentSettings { get; set; }
    public string InstallationDirectory { get; set; } = MainWindow.InstallPath;
    public bool ShowTerminal { get; set; }
    public int AutoUpdaterBehavior { get; set; }
    public string ConfigurationPath { get; set; } = AppData + "\\Launcher.json";
    public string LastReleaseFeedPrefix { get; set; } = AppData + "\\Release_";
    public string LastReleaseIndex { get; set; } = AppData + "\\LastIndex.json";
    public string NodePath { get; set; } = "C:\\Program Files\\nodejs\\node.exe";
    public string NpmPath { get; set; } = "C:\\Program Files\\nodejs\\npm.cmd";
    public string GitPath { get; set; } = "C:\\Program Files\\Git\\bin\\git.exe";
    public string ServerUrl { get; set; } = "http://localhost:5754";
}