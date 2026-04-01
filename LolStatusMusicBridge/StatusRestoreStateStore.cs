using System.Text.Json;

namespace LolStatusMusicBridge;

internal sealed class StatusRestoreStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public StatusRestoreStateStore()
    {
        var stateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LolStatusMusicBridge");

        Directory.CreateDirectory(stateDirectory);
        StateFilePath = Path.Combine(stateDirectory, "status-restore-state.json");
    }

    public string StateFilePath { get; }

    public StatusRestoreState? Load()
    {
        try
        {
            if (!File.Exists(StateFilePath))
            {
                return null;
            }

            using var stream = File.OpenRead(StateFilePath);
            return JsonSerializer.Deserialize<StatusRestoreState>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(StatusRestoreState state)
    {
        try
        {
            using var stream = new FileStream(
                StateFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            JsonSerializer.Serialize(stream, state, JsonOptions);
        }
        catch
        {
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(StateFilePath))
            {
                File.Delete(StateFilePath);
            }
        }
        catch
        {
        }
    }
}
