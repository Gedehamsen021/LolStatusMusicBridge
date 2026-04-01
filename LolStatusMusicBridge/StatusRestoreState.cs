namespace LolStatusMusicBridge;

internal sealed record StatusRestoreState(
    string InitialStatusMessage,
    string LastAppliedStatusMessage);
