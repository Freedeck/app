﻿using System;

namespace Freedeck;

public class InstallInformation
{
    public string SourceServer { get; set; } = "https://releases.freedeck.app";
    public string SourceServerFile { get; set; } = "index.json";
    public string SourceChannel { get; set; } = "stable";
}

public class LauncherConfigSchema
{
    public static string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FreedeckApp";
    public bool PersistentSettings { get; set; }
    public string InstallationDirectory { get; set; } = MainWindow.InstallPath;
    public InstallInformation InstallationInformation { get; set; } = new();
    public bool ShowTerminal { get; set; }
    public bool ShowAutoupdaterWindow { get; set; }
    public int AutoUpdaterBehavior { get; set; }
    public string ConfigurationPath { get; set; } = AppData + "\\Launcher.json";
    public string LastReleaseIndex { get; set; } = AppData + "\\CacheMarketplaceManifest.json";
    public string CurrentInstalledBuild { get; set; } = AppData + "\\CurrentBuildID.txt";
    public string NodePath { get; set; } = "C:\\Program Files\\nodejs\\node.exe";
    public string NpmPath { get; set; } = "C:\\Program Files\\nodejs\\npm.cmd";
    public string GitPath { get; set; } = "C:\\Program Files\\Git\\bin\\git.exe";
    public string ServerUrl { get; set; } = "http://localhost:5754";
}