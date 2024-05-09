namespace BigMission.WrlDynoCheck.Services;

public interface ISettingsProvider
{
    string? GetAppSetting(string key);
}
