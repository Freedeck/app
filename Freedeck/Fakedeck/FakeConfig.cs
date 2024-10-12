using System;
using System.Text.Json;

namespace Freedeck.Fakedeck;

public class FakeConfig
{
    public static void test()
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

        var cfg = new ConfigBuilder()
            .SetWriteLogs(true)
            .SetRelease("stable")
            .SetTheme("default")
            .SetActiveProfile("TestCFG")
            .SetScreenSaverActivationTime(5)
            .SetSoundOnPress(false)
            .SetUseAuthentication(false)
            .SetIconCountPerPage(12)
            .SetPort(5754)
            .AddProfile("TestCFG", defaultProfile)
            .Build();
        
        Console.WriteLine($"Generated testing configuration. Config release: {cfg.release}, Active profile: {cfg.profile}");
        Console.WriteLine(FormatForFile(cfg));
    }

    public static string FormatForFile(FdConfig configuration)
    {
        string serialized = JsonSerializer.Serialize(configuration);
        return
            $"const cfg = {serialized}; if(typeof window !== 'undefined') window['cfg'] = cfg; if('exports' in module) module.exports = cfg;";
    }
}