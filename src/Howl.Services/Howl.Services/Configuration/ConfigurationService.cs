using System;
using System.IO;
using System.Text.Json;

namespace Howl.Services.Configuration;

public class ConfigurationService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Howl"
    );

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    public class HowlConfig
    {
        public string? ApiKey { get; set; }
    }

    public static HowlConfig LoadConfiguration()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<HowlConfig>(json);
                return config ?? new HowlConfig();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load configuration: {ex.Message}");
        }

        return new HowlConfig();
    }

    public static void SaveConfiguration(HowlConfig config)
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(ConfigDirectory);

            // Serialize to JSON with indentation for readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    public static string GetConfigFilePath()
    {
        return ConfigFilePath;
    }

    public static void ClearApiKey()
    {
        var config = LoadConfiguration();
        config.ApiKey = null;
        SaveConfiguration(config);
    }
}
