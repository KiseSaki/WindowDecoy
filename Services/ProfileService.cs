using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WindowDecoy.Models;

namespace WindowDecoy.Services;

public static class ProfileService
{
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowDecoy");

    private static readonly string ConfigFile = Path.Combine(AppFolder, "profiles.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static List<DecoyProfile> LoadProfiles()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                string json = File.ReadAllText(ConfigFile);
                var profiles = JsonSerializer.Deserialize<List<DecoyProfile>>(json, JsonOptions);
                if (profiles != null && profiles.Count > 0)
                    return profiles;
            }
        }
        catch
        {
            // Fallback to default
        }

        return GetDefaultProfiles();
    }

    public static void SaveProfiles(List<DecoyProfile> profiles)
    {
        try
        {
            if (!Directory.Exists(AppFolder))
            {
                Directory.CreateDirectory(AppFolder);
            }

            string json = JsonSerializer.Serialize(profiles, JsonOptions);
            File.WriteAllText(ConfigFile, json);
        }
        catch
        {
            // Ignore write errors or log
        }
    }

    public static List<DecoyProfile> GetDefaultProfiles()
    {
        return new List<DecoyProfile>
        {
            new()
            {
                Name = "VS Code 伪装 (默认)",
                FakeTitle = "UserMapper.java - 小程序 - Visual Studio Code",
                SizeMode = WindowSizeMode.TargetWindow,
                PositionMode = WindowPositionMode.TargetWindow,
                BehaviorMode = DecoyBehaviorMode.Proxy,
                Hotkey = "Ctrl+Alt+Q",
                RestoreHotkey = "Ctrl+Alt+`"
            }
        };
    }
}