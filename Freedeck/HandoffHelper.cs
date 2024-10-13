using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Interactivity;

namespace Freedeck;

public class HandoffHelper
{
    public static void Initialize()
    {
        MainWindow.Instance.TabHandoff.IsVisible = false;
        MainWindow.Instance.THandoffProgress.IsVisible = false;
        MainWindow.Instance.THandoffNo.Click += GenericNo;
        MainWindow.Instance.THandoffYes.Click += DownloadYes;
    }
    public static void HandleCommand(string raw)
    {
        bool validCommand = true;
        string[] args = raw.Split("freedeck://")[1].Split("/");
        string command = args[0];
        switch (command)
        {
            case "download":
            case "update":
                if (args.Length < 5)
                {
                    Console.WriteLine("Received malformed command");
                    return;
                }
                string id = args[1];
                string url = Uri.UnescapeDataString(args[2]);
                string description = Uri.UnescapeDataString(args[3]);
                string repoTitle = Uri.UnescapeDataString(args[4]);
                if(command.Equals("download")) DownloadPluginRequest(url, id, description, repoTitle);
                else
                {
                    string from = Uri.UnescapeDataString(args[5]);
                    string to = Uri.UnescapeDataString(args[6]);
                    UpdatePluginRequest(url, id, description, repoTitle, from, to);
                }
                break;
            default:
                validCommand = false;
                break;
        }

        if (!validCommand) return;
        if (!MainWindow.Instance.IsVisible)
        {
            MainWindow.Instance.IsVisible = true;
        } 
        MainWindow.Instance.TabHandoff.IsVisible = true;
        MainWindow.Instance.TabHandoff.IsSelected = true;
        TakeAccess();
    }

    private static void TakeAccess()
    {
        MainWindow.Instance.TabRun.IsEnabled = false;
        MainWindow.Instance.TabSettings.IsEnabled = false;
        MainWindow.Instance.TabConfig.IsEnabled = false;
        MainWindow.Instance.TabMarketplace.IsEnabled = false;
    }

    private static void GiveAccess()
    {
        MainWindow.Instance.TabRun.IsEnabled = true;
        MainWindow.Instance.TabSettings.IsEnabled = true;
        MainWindow.Instance.TabConfig.IsEnabled = true;
        MainWindow.Instance.TabMarketplace.IsEnabled = true;
    }

    private static string _currentDownloadId = null!;
    private static string _currentDownloadUrl = null!;

    public static void DownloadPluginRequest(string url, string id, string description, string repoTitle)
    {
        _currentDownloadUrl = url;
        _currentDownloadId = id;
        MainWindow.Instance.THandoffPrompt.Text = $"Download {id} from {repoTitle}?";
        MainWindow.Instance.THandoffDescription.Text = description;
        MainWindow.Instance.THandoffUrl.Text = $"from {url}";
        MainWindow.Instance.THandoffTrust.Text = "This is an unknown source; so be careful.";
        if (url.StartsWith("https://content-dl.freedeck.app/"))
            MainWindow.Instance.THandoffTrust.Text = "This is an official source, and can be trusted.";
    }
    public static void UpdatePluginRequest(string url, string id, string description, string repoTitle, string from, string to)
    {
        _currentDownloadUrl = url;
        _currentDownloadId = id;
        MainWindow.Instance.THandoffPrompt.Text = $"Update {id} using {repoTitle}?";
        MainWindow.Instance.THandoffDescription.Text = description + $"\n\nUpdating from v{from} to v{to}";
        MainWindow.Instance.THandoffUrl.Text = $"from {url}";
        MainWindow.Instance.THandoffTrust.Text = "This is an unknown source; so be careful.";
        if (url.StartsWith("https://content-dl.freedeck.app/"))
            MainWindow.Instance.THandoffTrust.Text = "This is an official source, and can be trusted.";
    }

    private static void GenericNo(object? sender, RoutedEventArgs args)
    {
        MainWindow.Instance.TabHandoff.IsVisible = false;
        MainWindow.Instance.TabRun.IsSelected = true;
        MainWindow.Instance.IsVisible = false;
        GiveAccess();
    }
    
    private static void DownloadYes(object? sender, RoutedEventArgs args)
    {
        MainWindow.Instance.THandoffProgress.IsVisible = true;
        HttpClient httpClient = new HttpClient();
        httpClient.GetAsync(
            LauncherConfig.Configuration.ServerUrl +
            "/handoff/" +GetHandoffToken()+ 
            "/notify/Downloading " + _currentDownloadId);
        DownloadIt(_currentDownloadId, _currentDownloadUrl, httpClient);
        GiveAccess();
    }

    private static void DownloadIt(string id, string url, HttpClient httpClient)
    {
        WebClient wc = new WebClient();
        wc.DownloadProgressChanged += (sender, args) =>
        {
            MainWindow.Instance.THandoffProgress.Value = args.ProgressPercentage;
        };
        wc.DownloadFileCompleted += async (sender, args) =>
        {
            MainWindow.Instance.THandoffProgress.IsVisible = false;
            MainWindow.Instance.TabHandoff.IsVisible = false;
            MainWindow.Instance.TabRun.IsSelected = false;
            MainWindow.Instance.Hide();
            await httpClient.GetAsync(
                LauncherConfig.Configuration.ServerUrl +
                "/handoff/" + GetHandoffToken() +
                "/notify/Downloaded " + id + "!");
            await httpClient.GetAsync(
                LauncherConfig.Configuration.ServerUrl +
                "/handoff/" + GetHandoffToken() +
                "/reload-plugins");
        };
        wc.DownloadFileAsync(new Uri(url), 
            MainWindow.InstallPath + $"\\freedeck\\plugins\\{id}.Freedeck");
    }
    
    private static string GetHandoffToken()
    {
        HttpClient httpClient = new HttpClient();
        Task<string> s = httpClient.GetStringAsync(LauncherConfig.Configuration.ServerUrl + "/handoff/get-token");
        return s.Result;
    }
}