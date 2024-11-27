
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using CSCore.CoreAudioAPI;

namespace Freedeck;

public class NBSApp
{
    public string DisplayName { get; set; }
    public string CurrentVolume { get; set; }
}
public class NativeBridge
{
    public static void setVolumeApp(string procName, float vol)
    {
        using var device = MMDeviceEnumerator.DefaultAudioEndpoint(
            DataFlow.Render,
            Role.Multimedia);

        using var master = AudioEndpointVolume.FromDevice(device);

        using var sessionManager = AudioSessionManager2.FromMMDevice(device);
        using var enumerator = sessionManager.GetSessionEnumerator();

        //skipping first as it seems to be some default system session
        int i = 0;

        foreach (var sessionControl in enumerator.Skip(1))
        {
            using var sessionControl2 = sessionControl.QueryInterface<AudioSessionControl2>();

            using var process = Process.GetProcessById(sessionControl2.ProcessID);
            using var volume = sessionControl.QueryInterface<SimpleAudioVolume>();
            
            if (process.ProcessName == procName)
            {
                volume.MasterVolume = vol / 100.0f;
            }

        }
    }

    public static JsonArray GrabApps()
    {
        using var device = MMDeviceEnumerator.DefaultAudioEndpoint(
            DataFlow.Render,
            Role.Multimedia);

        using var master = AudioEndpointVolume.FromDevice(device);

        using var sessionManager = AudioSessionManager2.FromMMDevice(device);
        using var enumerator = sessionManager.GetSessionEnumerator();

        int i = 0;
        JsonArray apps = [];
        JsonObject masterApp = new JsonObject
        {
            { "name", "_fd.System" },
            { "volume", master.MasterVolumeLevelScalar }
        };
        apps.Add(masterApp);
        foreach (var sessionControl in enumerator.Skip(1))
        {
            using var sessionControl2 = sessionControl.QueryInterface<AudioSessionControl2>();

            using var process = Process.GetProcessById(sessionControl2.ProcessID);
            using var volume = sessionControl.QueryInterface<SimpleAudioVolume>();
                        
            String friendly = process.MainWindowTitle;
            if (process.ProcessName == "Idle") friendly = "System Sounds";
            
            JsonObject app = new JsonObject
            {
                { "name", process.ProcessName },
                { "friendly", friendly },
                { "volume", volume.MasterVolume }
            };
            apps.Add(app);
        }

        return apps;
    }
    public static void setVolumeMaster(float vol)
    {
        using var device = MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        using var master = AudioEndpointVolume.FromDevice(device);
        master.MasterVolumeLevelScalar = vol / 100.0f;
    }
    public static void Initialize()
    {
        if (!OperatingSystem.IsWindows()) return;
        Task.Run(async () =>
        {
            var server = new NativeBridgeServer("http://localhost:5756/");
            server.ListenForEvent("get_apps", (WebSocket socket, string[] data) =>
            {
                 JsonArray apps = GrabApps();

                server.SendFDPacket(new NBSDataStructure()
                {
                    Event = "apps",
                    Data = new string[] { apps.ToString() }
                }, socket);
            });
            server.ListenForEvent("macro", ((socket, strings) =>
            {
                
            }));
            server.ListenForEvent("hello", (WebSocket socket, string[] data) =>
            {
                Console.WriteLine("Hello from client");
                server.SendFDPacket(new NBSDataStructure()
                {
                    Event = "hello",
                    Data = new string[] { "Hello from the server!" }
                }, socket);
            });
            server.ListenForEvent("set_volume", (WebSocket socket, string[] data) =>
            {
                Console.WriteLine("Setting volume");
                Console.WriteLine(data);
                if (data[0] == "_fd.System")
                {
                    setVolumeMaster(float.Parse(data[1]));
                }
                else
                {
                    setVolumeApp(data[0], float.Parse(data[1]));
                }
                
                JsonArray apps = GrabApps();
                
                server.SendFDPacket(new NBSDataStructure()
                {
                    Event = "volume_set",
                    Data = new string[] { apps.ToString() }
                }, socket);
            });
            Console.WriteLine("Started NBWS server");
            await server.StartAsync();
        });
    }
}