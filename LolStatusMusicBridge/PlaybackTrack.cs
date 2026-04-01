namespace LolStatusMusicBridge;

internal sealed record PlaybackTrack(string Title, string Artist, string SourceAppUserModelId)
{
    public string DisplayText =>
        string.IsNullOrWhiteSpace(Artist) ? Title : $"{Artist} - {Title}";
}
