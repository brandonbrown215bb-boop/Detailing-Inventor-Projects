using FileGrid.Portable.Models;

namespace FileGrid.Portable.Services;

public sealed class SettingsRepository
{
    public AppSettings Load()
    {
        var s = JsonStore.Read<AppSettings>(AppPaths.SettingsFile);
        return s ?? new AppSettings();
    }

    public void Save(AppSettings settings) => JsonStore.Write(AppPaths.SettingsFile, settings);
}

public sealed class SessionRepository
{
    public SessionMeta Load() => JsonStore.Read<SessionMeta>(AppPaths.SessionFile) ?? new SessionMeta();

    public void Save(SessionMeta meta) => JsonStore.Write(AppPaths.SessionFile, meta);
}
