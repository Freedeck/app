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
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace Freedeck;


public class ReleaseVersioningChannel
{
    public string id { get; set; }
    public string type { get; set; }
    public string source { get; set; }
    public string branch { get; set; }
    public string description { get; set; }
    public JsonElement catalog { get; set; }
    public bool? error { get; set; } = false;
    public string? error_message { get; set; } = "none";
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
        await Dispatcher.UIThread.InvokeAsync(() => { MainWindow.Instance.OfflineStatus.IsVisible = !IsOnline; });
    }

    public static string Server = "https://releases.freedeck.app";
    public static string File = "index.json";
    public static bool IsOnline;
    private static ReleaseIndexFile _index = new();

    public static async Task FullyUpdate()
    {
        Server = LauncherConfig.Configuration.InstallationInformation.SourceServer;
        File = LauncherConfig.Configuration.InstallationInformation.SourceServerFile;
        Dispatcher.UIThread.InvokeAsync(() => { MainWindow.Instance.ProgressBarContainer.IsVisible = true; });
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
        string path = $"{Server}/{File}";
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarCurrently.Text = $"Connecting to {Server}/{File}";
        });

        using HttpClient hc = new();
        try
        {
            Dispatcher.UIThread.InvokeAsync(() => { MainWindow.Instance.ProgressBarApp.Value = 40; });

            string fileContents = await hc.GetStringAsync(path);
            IsOnline = true;
            CheckOnline();

            // Write backup
            _ = Task.Run(() =>
            {
                System.IO.File.WriteAllTextAsync(LauncherConfig.Configuration.LastReleaseIndex,
                    fileContents);
            });

            // Parse JSON manually to handle the object structure
            using var document = JsonDocument.Parse(fileContents);
            var root = document.RootElement;

            var channels = new List<ReleaseVersioningChannel>();

            // Iterate over each property in the "channels" object
            foreach (var channelProperty in root.GetProperty("channels")
                         .EnumerateObject())
            {
                var channelElement = channelProperty.Value;
                var type = channelElement.GetProperty("type")
                    .GetString();

                // Process only "release" type channels
                if (type == "release")
                {
                    var releaseChannel =
                        JsonSerializer.Deserialize<ReleaseVersioningChannel>(channelElement.GetRawText());
                    releaseChannel.id = channelProperty.Name;
                    channels.Add(releaseChannel);
                }
            }

            _index = new ReleaseIndexFile
            {
                channels = channels
            };

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.ProgressBarApp.Value = 50;
                MainWindow.Instance.ProgressBarCurrently.Text = $"Setting up UI...";
            });
        }
        catch (Exception ex)
        {
            IsOnline = false;
            CheckOnline();
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindow.Instance.ProgressBarApp.Value = 20;
                MainWindow.Instance.ProgressBarCurrently.Text = $"Couldn't connect. Attempting to use local backup.";
            });
            Console.WriteLine($"Error fetching index: {ex.Message}");
            Console.WriteLine($"Server: {Server}/{File}");
            Console.WriteLine($"Attempting to use backup from {LauncherConfig.Configuration.LastReleaseIndex}");

            if (System.IO.File.Exists(LauncherConfig.Configuration.LastReleaseIndex))
            {
                string f = System.IO.File.ReadAllText(LauncherConfig.Configuration.LastReleaseIndex);
                using var backupDocument = JsonDocument.Parse(f);
                var root = backupDocument.RootElement;

                var channels = new List<ReleaseVersioningChannel>();

                foreach (var channelProperty in root.GetProperty("channels")
                             .EnumerateObject())
                {
                    var channelElement = channelProperty.Value;
                    var type = channelElement.GetProperty("type")
                        .GetString();

                    if (type == "release")
                    {
                        var releaseChannel =
                            JsonSerializer.Deserialize<ReleaseVersioningChannel>(channelElement.GetRawText());
                        releaseChannel.id = channelProperty.Name;
                        channels.Add(releaseChannel);
                    }
                }

                _index = new ReleaseIndexFile
                {
                    channels = channels
                };
                Console.WriteLine("Instantiated backup index.");
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainWindow.Instance.ProgressBarApp.Value = 0;
                    MainWindow.Instance.ProgressBarCurrently.Text =
                        $"Couldn't find a backup index. Please go online and retry to download it.";
                });
                Console.WriteLine($"No backup found for {Server}/{File}'s index.");
            }
        }
    }

    public static string GetLatestVersionFor(ReleaseVersioningChannel channel)
    {
        foreach (var release in _index.channels)
        {
            if (release.id == channel.id)
            {
                foreach (var version in JsonSerializer.Deserialize<Release[]>(release.catalog.GetRawText(),
                             new JsonSerializerOptions
                             {
                                 PropertyNameCaseInsensitive = true
                             }))
                {
                    if (version.current == true)
                        return version.version;
                }
            }
        }

        return "v0.0.0";
    }

    public static Task<ReleaseVersioningChannel> GetChannel(string id)
    {
        foreach (var release in _index.channels)
        {
            if (release.id == id)
            {
                return Task.FromResult(release);
            }
        }

        return Task.FromResult(new ReleaseVersioningChannel(){error=true, error_message = "Could not find release channel " + id +"."});
    }

    public static async Task UpdateCatalogAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ComboBox selector = MainWindow.Instance.SChannelSelector;
            selector.Items.Clear();
            foreach (ReleaseVersioningChannel channel in _index.channels)
            {
                ComboBoxItem item = new ComboBoxItem
                {
                    Content = $"{channel.description} ({channel.id})",
                    Tag = channel.id
                };
                selector.Items.Add(item);
                if (item.Tag.ToString() == LauncherConfig.Configuration.InstallationInformation.SourceChannel)
                    selector.SelectedItem = item;
            }
        });
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            MainWindow.Instance.ProgressBarApp.Value = 90;
            MainWindow.Instance.ProgressBarCurrently.Text = "Loading catalog...";
            MainWindow.Instance.ReleaseCatalogs.Children.Clear();

            string selectedChannelId = LauncherConfig.Configuration.InstallationInformation.SourceChannel;

            var wantedChannel = _index.channels.Find(x => x.id == selectedChannelId);
            if (wantedChannel == null)
            {
                var containerTwo = new StackPanel();
                var errorMsg = new TextBlock
                {
                    Text = $"Couldn't find release channel {selectedChannelId}.",
                    TextAlignment = TextAlignment.Center
                };
                containerTwo.Children.Add(errorMsg);
                MainWindow.Instance.ReleaseCatalogs.Children.Add(containerTwo);

                MainWindow.Instance.ProgressBarApp.Value = 100;
                MainWindow.Instance.ProgressBarCurrently.Text = "";
                MainWindow.Instance.ProgressBarContainer.IsVisible = false;
                return Task.CompletedTask;
            }

            var catalog = JsonSerializer.Deserialize<Release[]>(wantedChannel.catalog.GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            string title = wantedChannel.description;
            Console.WriteLine($"Creating catalog for {title}. {catalog.Length} versions found.");

            var border = new Border
            {
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(10),
                Background = new SolidColorBrush(Color.FromArgb(0,
                    0,
                    0,
                    0))
            };

            var container = new StackPanel();
            border.Child = container;

            var titleText = new TextBlock
            {
                Text = title,
                TextAlignment = TextAlignment.Center
            };
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
                Console.WriteLine($"Creating {version.version} - {version.desc}");
                var verBorder = new Border
                {
                    CornerRadius = new CornerRadius(15),
                    Width = (version.current == true
                        ? 250
                        : 200),
                    Height = 70,
                    BorderThickness = new Thickness(10),
                    Background = new SolidColorBrush(Color.FromArgb(32,
                        0,
                        0,
                        0))
                };

                var textBlock = new TextBlock
                {
                    FontSize = (version.current == true
                        ? 20
                        : 12),
                    Text = $"v{version.version}\n{version.desc}",
                    TextAlignment = TextAlignment.Center
                };
                if (version.current == true && MainWindow.AppVersion != version.version)
                {
                    textBlock.Text = $"v{version.version}\nUpdate available!";
                    verBorder.Background = new SolidColorBrush(Color.FromArgb(20,
                        0,
                        0,
                        0));
                }
                else if (version.current == true && MainWindow.AppVersion == version.version)
                {
                    verBorder.Background = new SolidColorBrush(Color.FromArgb(40,
                        60,
                        255,
                        60));
                }

                verBorder.Child = textBlock;
                versionContainer.Children.Add(verBorder);
            }

            MainWindow.Instance.ReleaseCatalogs.Children.Add(border);

            MainWindow.Instance.ProgressBarApp.Value = 100;
            MainWindow.Instance.ProgressBarCurrently.Text = "";
            MainWindow.Instance.ProgressBarContainer.IsVisible = false;
            return Task.CompletedTask;
        });
    }

}