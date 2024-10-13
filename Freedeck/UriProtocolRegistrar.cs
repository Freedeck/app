using Microsoft.Win32;
using System;

namespace Freedeck;

public static class UriProtocolRegistrar
{
    public static void RegisterUriScheme(string protocol, string appPath, bool userLevel = true)
    {
        if(!OperatingSystem.IsWindows()) return;
        try
        {
            // Determine the base registry key
            RegistryKey baseKey = (userLevel 
                ? Registry.CurrentUser.OpenSubKey("Software\\Classes", true) 
                : Registry.ClassesRoot)!;

            if (baseKey == null) throw new Exception("Failed to open the registry base key.");

            // Create or open the protocol key
            using var key = baseKey.CreateSubKey(protocol);
            if (key == null) throw new Exception("Failed to create the registry key for the protocol.");

            key.SetValue("", $"{protocol} Protocol");  // Display name
            key.SetValue("URL Protocol", "");  // Marks it as a URL protocol

            // Create the "shell\open\command" key
            using var shellKey = key.CreateSubKey(@"shell\open\command");
            if (shellKey == null) throw new Exception("Failed to create the shell\\open\\command key.");

            shellKey.SetValue("", $"\"{appPath}\" \"%1\"");  // Command with argument
            Console.WriteLine($"Successfully registered {protocol} protocol for {(userLevel ? "user" : "classes root")}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error registering protocol: {ex.Message}");
        }
    }
}
