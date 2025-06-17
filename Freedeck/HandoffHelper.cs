using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Interactivity;

namespace Freedeck;

public class HandoffHelper
{
    public static bool ActiveQuery = false;
    public static bool NativeOpened = false;
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
            case "native_open":
                MainWindow.Instance.Show();
                NativeOpened = true;
                break;
            case "exit":
                MainWindow.Instance.TabClose.IsVisible = true;
                MainWindow.Instance.TabClose.IsSelected = true;
                break;
            case "startup":
                NativeOpened = true;
                MainWindow.Instance.Hide();
                break;
            case "show":
                MainWindow.Instance.Show();
                if(args.Length > 1 && args[1] == "front")
                    App.BringToTop();
                GiveAccess();
                break;
            case "hide":
                MainWindow.Instance.Hide();
                GiveAccess();
                break;
            case "download":
            case "update":
                MainWindow.Instance.Show();
                App.BringToTop();
                if (!MainWindow.Instance.IsVisible)
                {
                    MainWindow.Instance.IsVisible = true;
                } 
                MainWindow.Instance.TabHandoff.IsVisible = true;
                MainWindow.Instance.TabHandoff.IsSelected = true;
                TakeAccess();
                if (args.Length < 5)
                {
                    Console.WriteLine(args.Length);
                    if (args.Length == 3 && command.Equals("download"))
                    {
                        string lid = args[1];
                        string lurl = Uri.UnescapeDataString(args[2]);
                        DownloadPluginRequestLegacy(lurl, lid);
                        break;
                    }
                    Console.WriteLine("Received malformed command");
                    validCommand = false;
                    break;
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
    }

    private static void TakeAccess()
    {
        MainWindow.Instance.TabRun.IsEnabled = false;
        MainWindow.Instance.TabSettings.IsEnabled = false;
        ActiveQuery = true;
    }

    private static void GiveAccess()
    {
        MainWindow.Instance.TabRun.IsEnabled = true;
        MainWindow.Instance.TabSettings.IsEnabled = true;
        ActiveQuery = false;
    }

    private static string _currentDownloadId = null!;
    private static string _currentDownloadUrl = null!;
    
    private static List<String> _officialPrefixes = new List<string>()
    {
        "https://content-dl.freedeck.app/",
        "https://freedeck.app/",
        "https://freedeck.github.io/plugins/"
    };

    public static bool IsFromOfficialSource(String url)
    {
        foreach (String prefix in _officialPrefixes)
        {
            if (url.StartsWith(prefix))
            {
                return true;
            }
        }
        return false;
    }

    public static void DownloadPluginRequestLegacy(string url, string id)
    {
        _currentDownloadUrl = url;
        _currentDownloadId = id;
        MainWindow.Instance.THandoffPrompt.Text = $"Download {id}?";
        MainWindow.Instance.THandoffDescription.Text = "This request comes from an older Freedeck version.";
        MainWindow.Instance.THandoffUrl.Text = $"from {url}";
        MainWindow.Instance.THandoffTrust.Text = "This is an unknown source; so be careful.";
        if (IsFromOfficialSource(url))
            MainWindow.Instance.THandoffTrust.Text = "This is an official source, and can be trusted.";
    }

    private static void DownloadPluginRequest(string url, string id, string description, string repoTitle)
    {
        _currentDownloadUrl = url;
        _currentDownloadId = id;
        MainWindow.Instance.THandoffPrompt.Text = $"Download {id} from {repoTitle}?";
        MainWindow.Instance.THandoffDescription.Text = description;
        MainWindow.Instance.THandoffUrl.Text = $"from {url}";
        MainWindow.Instance.THandoffTrust.Text = "This is an unknown source; so be careful.";
        if (IsFromOfficialSource(url))
            MainWindow.Instance.THandoffTrust.Text = "This is an official source, and can be trusted.";
    }

    private static void UpdatePluginRequest(string url, string id, string description, string repoTitle, string from, string to)
    {
        _currentDownloadUrl = url;
        _currentDownloadId = id;
        MainWindow.Instance.THandoffPrompt.Text = $"Update {id} using {repoTitle}?";
        MainWindow.Instance.THandoffDescription.Text = description + $"\n\nUpdating from v{from} to v{to}";
        MainWindow.Instance.THandoffUrl.Text = $"from {url}";
        MainWindow.Instance.THandoffTrust.Text = "This is an unknown source; so be careful.";
        if (IsFromOfficialSource(url))
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
        ActiveQuery = false;
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
        string extension = url.Split(".")[url.Split(".").Length - 1];
        wc.DownloadFileAsync(new Uri(url), 
            MainWindow.InstallPath + $"\\freedeck\\plugins\\{id}.{extension}");
    }
    
    private static string GetHandoffToken()
    {
        HttpClient httpClient = new HttpClient();
        Task<string> s = httpClient.GetStringAsync(LauncherConfig.Configuration.ServerUrl + "/handoff/get-token");
        return s.Result;
    }
}