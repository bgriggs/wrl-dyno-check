using Microsoft.Extensions.Configuration;
using System.IO;

namespace BigMission.WrlDynoCheck.Services;

public class WindowsSettingsProvider : ISettingsProvider
{
    private readonly IConfiguration? appSettings;
    private readonly string appSettingsFile = "appsettings.json";

    public WindowsSettingsProvider()
    {
        var dir = Directory.GetCurrentDirectory();
        var appSettingsPath = Path.Combine(dir, appSettingsFile);
        if (!string.IsNullOrEmpty(appSettingsPath) && File.Exists(appSettingsPath))
        {
            var cb = new ConfigurationBuilder();
            cb.AddJsonFile(appSettingsPath);
            appSettings = cb.Build();
        }
    }

    public string? GetAppSetting(string key)
    {
        return appSettings?.GetValue<string>(key);
    }
}
