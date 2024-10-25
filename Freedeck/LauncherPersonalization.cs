using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media;

namespace Freedeck;

public class Theme
{
    public byte TransparencyValue;
    public byte r;
    public byte g;
    public byte b;

    public Theme(byte transparencyValue, byte r, byte g, byte b)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        TransparencyValue = transparencyValue;
    }
}

public class LauncherPersonalization
{
    public static Dictionary<String, Theme> Themes = new()
    {
        { "default", new Theme(255, 23, 33, 42) },
        { "black", new Theme(255,0,0,0) },
        { "catppuccin_mocha", new Theme(255,30,30,40)},
        { "dark", new Theme(242, 0,0,0)},
        { "fun", new Theme(100,0,0,255)},
        { "blue", new Theme(255, 0, 183, 255)},
        { "gold", new Theme(255, 255, 215, 59)},
        { "green", new Theme(255, 51, 255, 119)},
        { "gruggly", new Theme(255, 0x4a, 0x41, 0x2A)},
        { "red", new Theme(255, 140,0,89)},
        { "soofle", new Theme(255, 220,56,72)}
    };
    public static void Initialize()
    {
        if (File.Exists(MainWindow.InstallPath + "\\freedeck\\src\\configs\\config.fd.js"))
        {
            string fc = File.ReadAllText(MainWindow.InstallPath + "\\freedeck\\src\\configs\\config.fd.js");
            string want = "theme";
            if (!fc.Contains("theme:")) want = "\"theme\"";
            string currentTheme = fc.Split(want+":\"")[1].Split("\"")[0];
            if (currentTheme.Equals("default")) return;
            MainWindow window = MainWindow.Instance;
            Color color = Color.FromRgb(23, 33, 42);
            if (Themes.TryGetValue(currentTheme, out var ct))
            {
                color = new Color(ct.TransparencyValue, ct.r, ct.g, ct.b);
            }
            window.Background = new SolidColorBrush(color);
        }
    }
}