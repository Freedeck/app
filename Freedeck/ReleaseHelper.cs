using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace Freedeck;

public class ReleaseMarketplaceRepository
{
    public string title { get; set; }
    public string description { get; set; }
    public string folder { get; set; }
    public string index { get; set; }
}

public class ReleaseChannel
{
    public string id { get; set; }
    public string description { get; set; }
    public string version { get; set; }
    public string catalog { get; set; }
}

public class ReleaseIndexFile
{
    public string url { get; set; }
    public ReleaseMarketplaceRepository[] marketplace_repository { get; set; }
    public ReleaseChannel[] release_channels { get; set; }
}

public class ReleaseVersionCatalogVersion
{
    public string version { get; set; }
    public string desc { get; set; }
}

public class ReleaseVersionCatalog
{
    public string title { get; set; } = "No Channel Name";
    public ReleaseVersionCatalogVersion[] versions { get; set; }
}

public class ReleaseHelper
{
    public static async void CheckOnline()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.OfflineStatus.IsVisible = !isOnline;
        });
    }
    
    public static string server = "https://releases.freedeck.app";
    public static string file = "index.json";
    public static bool isOnline = false;
    public static ReleaseIndexFile index = new ReleaseIndexFile();
    public static Dictionary<string, ReleaseVersionCatalog> catalogs;

    public static async Task FullyUpdate()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarContainer.IsVisible = true;
        });
        await GetLatestIndexAsync();
        await GetLatestReleaseCatalogAsync();
        await UpdateCatalogAsync();
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarCurrently.Text = "";
            MainWindow.Instance.ProgressBarApp.Value = 0;
            MainWindow.Instance.ProgressBarContainer.IsVisible = true;
        });
    }

    public static async Task GetLatestIndexAsync()
    {
        string path = $"{server}/{file}";
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarCurrently.Text = $"Connecting to {server}/{file}";
        });
        using HttpClient hc = new();
        try
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.ProgressBarApp.Value = 40;
                MainWindow.Instance.ProgressBarCurrently.Text = $"Got index. Found {index.release_channels.Length} channels.";
            });
            string fileContents = await hc.GetStringAsync(path);
            isOnline = true;
            CheckOnline();
            _ = Task.Run(() =>
            {
                File.WriteAllTextAsync(LauncherConfig.Configuration.LastReleaseIndex, fileContents);
            });
            index = JsonSerializer.Deserialize<ReleaseIndexFile>(fileContents)!;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.ProgressBarApp.Value = 50;
                MainWindow.Instance.ProgressBarCurrently.Text = $"Setting up UI...";
            });
        }
        catch (Exception ex)
        {
            isOnline = false;
            CheckOnline();
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.ProgressBarApp.Value = 20;
                MainWindow.Instance.ProgressBarCurrently.Text = $"Couldn't connect. Attempting to use local backup.";
            });
            Console.WriteLine($"Error fetching index: {ex.Message}");
            Console.WriteLine($"Server: {server}/{file}");
            Console.WriteLine($"Attempting to use backup from {LauncherConfig.Configuration.LastReleaseIndex}");

            if (File.Exists(LauncherConfig.Configuration.LastReleaseIndex))
            {
                string f = File.ReadAllText(LauncherConfig.Configuration.LastReleaseIndex);
                index = JsonSerializer.Deserialize<ReleaseIndexFile>(f)!;
                Console.WriteLine("Instantiated backup index.");
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainWindow.Instance.ProgressBarApp.Value = 0;
                    MainWindow.Instance.ProgressBarCurrently.Text = $"Couldn't find a backup index. Please go online and retry to download it.";
                });
                Console.WriteLine($"No backup found for {server}/{file}'s index.");
            }
        }
    }

    public static async Task GetLatestReleaseCatalogAsync()
    {
        if (index.release_channels == null)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.ProgressBarCurrently.Text = "No release channels found.";
            });
            return;
        };

        if (catalogs == null)
            catalogs = new Dictionary<string, ReleaseVersionCatalog>();

        using HttpClient hc = new();
        foreach (var releaseChannel in index.release_channels)
        {
            try
            {
                string path = $"{server}/{releaseChannel.catalog}";
                Console.WriteLine(path);
                string catalogContent = await hc.GetStringAsync(path);
                isOnline = true;
                CheckOnline();
                _ = Task.Run(() =>
                {
                    File.WriteAllTextAsync(LauncherConfig.Configuration.LastReleaseFeedPrefix + $"{releaseChannel.id}.json", catalogContent);
                });
                var parsedCatalog = JsonSerializer.Deserialize<ReleaseVersionCatalog>(catalogContent);
                catalogs[releaseChannel.id] = parsedCatalog!;
                foreach (var releaseVersionCatalogVersion in parsedCatalog.versions)
                {
                    Console.WriteLine("Found " + releaseVersionCatalogVersion.version +" - " + releaseVersionCatalogVersion.desc);
                }
            }
            catch (Exception ex)
            {
                isOnline = false;
                CheckOnline();
                Console.WriteLine($"Error fetching catalog for {releaseChannel.id}: {ex.Message}");
                Console.WriteLine("Attempting to fallback to " + LauncherConfig.Configuration.LastReleaseFeedPrefix+$"{releaseChannel.id}.json");

                if (File.Exists( LauncherConfig.Configuration.LastReleaseFeedPrefix+$"{releaseChannel.id}.json"))
                {
                    string f = File.ReadAllText( LauncherConfig.Configuration.LastReleaseFeedPrefix+$"{releaseChannel.id}.json");
                    var parsedCatalog = JsonSerializer.Deserialize<ReleaseVersionCatalog>(f);
                    catalogs[releaseChannel.id] = parsedCatalog!;
                    foreach (var releaseVersionCatalogVersion in parsedCatalog.versions)
                    {
                        Console.WriteLine("Found " + releaseVersionCatalogVersion.version +" - " + releaseVersionCatalogVersion.desc);
                    }
                }
                else
                {
                    Console.WriteLine($"No backup catalog found for {releaseChannel.id}");
                }
            }
        }
    }

    public static async Task UpdateCatalogAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarApp.Value = 100;
            MainWindow.Instance.ProgressBarCurrently.Text = "";
            foreach (var releaseVersionCatalog in catalogs)
            {
                var catalog = releaseVersionCatalog.Value;
                string title = catalog.title;
                Console.WriteLine($"Creating catalog for {releaseVersionCatalog.Key}. {catalog.versions.Length} versions found.");

                var border = new Border
                {
                    CornerRadius = new CornerRadius(15),
                    BorderThickness = new Thickness(10),
                    Background = new SolidColorBrush(Color.FromArgb(32, 0, 0, 0))
                };

                var container = new StackPanel();
                border.Child = container;

                var titleText = new TextBlock { Text = title };
                container.Children.Add(titleText);

                var scrollViewer = new ScrollViewer
                {
                    Height = 250
                };
                var versionContainer = new StackPanel();
                container.Children.Add(scrollViewer);
                scrollViewer.Content = versionContainer;

                foreach (var version in catalog.versions)
                {
                    Console.WriteLine($"Creating {version.version} - {version.desc}");
                    var verBorder = new Border
                    {
                        CornerRadius = new CornerRadius(15),
                        Width = (MainWindow.AppVersion == version.version ? 500 : 400),
                        Height = 70,
                        BorderThickness = new Thickness(10),
                        Background = new SolidColorBrush(Color.FromArgb(32, 0, 0, 0))
                    };

                    var textBlock = new TextBlock
                    {
                        FontSize = (MainWindow.AppVersion == version.version ? 24 : 16),
                        Text = $"v{version.version} - {version.desc}"
                    };
                    verBorder.Child = textBlock;
                    versionContainer.Children.Add(verBorder);
                }

                MainWindow.Instance.ReleaseCatalogs.Children.Add(border);
            }

            MainWindow.Instance.ProgressBarContainer.IsVisible = false;
            return Task.CompletedTask;
        });
    }
}