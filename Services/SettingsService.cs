using System.IO;
using System.Text.Json;
using ModbusChart.Models;

namespace ModbusChart.Services;

public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LFTMonitor");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (s != null)
                {
                    // Ensure all 8 channels exist (fill missing ones from defaults)
                    var defaults = AppSettings.CreateDefaultChannels();
                    foreach (var def in defaults)
                    {
                        if (!s.Channels.Any(c => c.Id == def.Id))
                            s.Channels.Add(def);
                    }
                    return s;
                }
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
