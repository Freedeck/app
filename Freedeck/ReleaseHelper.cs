using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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


public class ReleaseVersioningChannel
{
    public string id { get; set; }
    public string type { get; set; }
    public string description { get; set; }
    public JsonElement catalog { get; set; }
}

public class ReleaseIndexFile
{
    public List<ReleaseVersioningChannel> channels { get; set; } = new();
}

public class Release
{
    public bool? current { get; set; } = false;
    public string version { get; set; }
    public string desc { get; set; }
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
    public static string latestVersion = "v6.0.0";
    public static bool isOnline = false;
    public static ReleaseIndexFile index = new ReleaseIndexFile();

    public static async Task FullyUpdate()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarContainer.IsVisible = true;
        });
        await GetLatestIndexAsync();
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
            });

            string fileContents = await hc.GetStringAsync(path);
            isOnline = true;
            CheckOnline();

            // Write backup
            _ = Task.Run(() =>
            {
                File.WriteAllTextAsync(LauncherConfig.Configuration.LastReleaseIndex, fileContents);
            });

            // Parse JSON manually to handle the object structure
            using var document = JsonDocument.Parse(fileContents);
            var root = document.RootElement;

            var channels = new List<ReleaseVersioningChannel>();

            // Iterate over each property in the "channels" object
            foreach (var channelProperty in root.GetProperty("channels").EnumerateObject())
            {
                var channelElement = channelProperty.Value;
                var type = channelElement.GetProperty("type").GetString();

                // Process only "release" type channels
                if (type == "release")
                {
                    var releaseChannel = JsonSerializer.Deserialize<ReleaseVersioningChannel>(channelElement.GetRawText());

                    channels.Add(releaseChannel);
                }
            }

            index = new ReleaseIndexFile { channels = channels};

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
                using var backupDocument = JsonDocument.Parse(f);
                var root = backupDocument.RootElement;

                var channels = new List<ReleaseVersioningChannel>();

                foreach (var channelProperty in root.GetProperty("channels").EnumerateObject())
                {
                    var channelElement = channelProperty.Value;
                    var type = channelElement.GetProperty("type").GetString();

                    if (type == "release")
                    {
                        var releaseChannel = JsonSerializer.Deserialize<ReleaseVersioningChannel>(channelElement.GetRawText());
                        channels.Add(releaseChannel);
                    }
                }

                index = new ReleaseIndexFile { channels = channels };
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




    public static async Task UpdateCatalogAsync()
{
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
        MainWindow.Instance.ProgressBarApp.Value = 100;
        MainWindow.Instance.ProgressBarCurrently.Text = "";

        foreach (var releaseVersionCatalog in index.channels)
        {
            // Deserialize the catalog only for release channels
            var catalog = JsonSerializer.Deserialize<Release[]>(releaseVersionCatalog.catalog.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            string title = releaseVersionCatalog.description;
            Console.WriteLine($"Creating catalog for {title}. {catalog.Length} versions found.");

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

            foreach (var version in catalog)
            {
                if(version.current == true) latestVersion = version.version;
                Console.WriteLine($"Creating {version.version} - {version.desc}");
                var verBorder = new Border
                {
                    CornerRadius = new CornerRadius(15),
                    Width = ((MainWindow.AppVersion == version.version || version.current == true) ? 500 : 400),
                    Height = 70,
                    BorderThickness = new Thickness(10),
                    Background = new SolidColorBrush(Color.FromArgb(32, 0, 0, 0))
                };

                var textBlock = new TextBlock
                {
                    FontSize = ((MainWindow.AppVersion == version.version || version.current == true) ? 24 : 16),
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