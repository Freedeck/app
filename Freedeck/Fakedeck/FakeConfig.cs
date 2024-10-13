using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Freedeck.Fakedeck;

public class FakeConfig
{
    public static void CreateDefaultConfiguration()
    {
        var defaultProfile = new ProfileBuilder()
            .AddEntry("Welcome",
                new TileBuilder()
                    .SetType("fd.none")
                    .SetPosition(0)
                    .SetUUID("fdc.9539141.899567483")
                    .Build())
            .AddEntry("to",
                new TileBuilder()
                    .SetType("fd.none")
                    .SetPosition(1)
                    .SetUUID("fdc.8943922.745671902")
                    .AddData("color", "#b80846")
                    .Build())
            .AddEntry("Freedeck!", 
                new TileBuilder()
                    .SetType("fd.none")
                    .SetPosition(2)
                    .SetUUID("fdc.5193209.672054466")
                    .AddData("color", "#0585bb")
                    .Build())
            .AddEntry("Right click to get started.",
                new TileBuilder()
                    .SetType("fd.none")
                    .SetPosition(4)
                    .SetUUID("fdc.4734120.486344044")
                    .Build())
            .Build();
        var notDefaultProfile = new ProfileBuilder()
            .AddEntry("Welcome to Freedeck!",
                new TileBuilder()
                    .SetType("fd.none")
                    .SetPosition(0)
                    .SetUUID("fdc.9539141.899567483")
                    .Build())
            .Build();

        var cfg = new ConfigBuilder()
            .SetWriteLogs(true)
            .SetTheme("default")
            .SetActiveProfile("Default")
            .SetScreenSaverActivationTime(5)
            .SetSoundOnPress(false)
            .SetIconCountPerPage(12)
            .SetPort(5754)
            .SetUseAuthentication(false)
            .SetRelease(SetupLogic.IsChecked(MainWindow.Instance.SaRelease) ? "stable" : "dev")
            .AddProfile("Default", SetupLogic.IsChecked(MainWindow.Instance.SaWelcome) ? defaultProfile : notDefaultProfile)
            .Build();

        if (!Directory.Exists(MainWindow.InstallPath + "\\freedeck\\src\\configs"))
            Directory.CreateDirectory(MainWindow.InstallPath + "\\freedeck\\src\\configs");
        
        string hash = "fd.524c0321d302bd63cd4dcb56f0430b16be3cee5119dedc950271e1296944af83586326565db12b0a4caa65d7b83c8c11b738fc11b390a256f22f798fc72f7e1d";
        if (SetupLogic.IsChecked(MainWindow.Instance.SaAuthentication))
        {
            using (SHA512 shaM = SHA512.Create())
            {
                if (MainWindow.Instance.SaAuthenticationPassword.Text != null)
                {
                    byte[] rawHash = shaM.ComputeHash(Encoding.UTF8.GetBytes(MainWindow.Instance.SaAuthenticationPassword.Text));
                    hash = "fd.";
                    foreach (byte x in rawHash) {
                        hash += $"{x:x2}";
                    }
                }
            }
        }
        
        string fullCompleteSecrets = "const crypto = require('crypto');\nmodule.exports = {s:{password: '"+hash+"'},hash: (data) => 'fd.' + crypto.createHash('sha512').update(data).digest().toString('hex')};";
        File.WriteAllText(MainWindow.InstallPath + "\\freedeck\\src\\configs\\secrets.fd.js", fullCompleteSecrets);
        File.WriteAllText(MainWindow.InstallPath + "\\freedeck\\src\\configs\\config.fd.js", FormatForFile(cfg));
        
        Console.WriteLine($"Generated configuration. Config release: {cfg.release}, Active profile: {cfg.profile}");
    }

    private static string FormatForFile(FdConfig configuration)
    {
        string serialized = JsonSerializer.Serialize(configuration);
        return
            $"const cfg = {serialized}; if(typeof window !== 'undefined') window['cfg'] = cfg; if('exports' in module) module.exports = cfg;";
    }
}