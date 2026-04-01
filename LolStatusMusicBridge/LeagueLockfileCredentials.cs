namespace LolStatusMusicBridge;

internal sealed record LeagueLockfileCredentials(
    string LockfilePath,
    string ProcessName,
    int ProcessId,
    int Port,
    string Password,
    string Protocol)
{
    public Uri BaseUri => new($"{Protocol}://127.0.0.1:{Port}/");
}
