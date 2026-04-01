using Windows.Media.Control;

namespace LolStatusMusicBridge;

internal sealed class MusicSessionReader
{
    private readonly IReadOnlyList<string> _allowedSourceApps;

    public MusicSessionReader(AppOptions options)
    {
        _allowedSourceApps = options.AllowedSourceApps;
    }

    public async Task<PlaybackTrack?> TryGetCurrentTrackAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var currentSession = manager.GetCurrentSession();

            if (currentSession is not null)
            {
                var currentTrack = await TryCreateTrackAsync(currentSession);
                if (currentTrack is not null)
                {
                    return currentTrack;
                }
            }

            foreach (var session in manager.GetSessions())
            {
                if (currentSession is not null &&
                    session.SourceAppUserModelId == currentSession.SourceAppUserModelId)
                {
                    continue;
                }

                var track = await TryCreateTrackAsync(session);
                if (track is not null)
                {
                    return track;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<PlaybackTrack?> TryCreateTrackAsync(
        GlobalSystemMediaTransportControlsSession session)
    {
        var playbackInfo = session.GetPlaybackInfo();
        if (playbackInfo?.PlaybackStatus !=
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            return null;
        }

        if (!IsAllowedSourceApp(session.SourceAppUserModelId))
        {
            return null;
        }

        var mediaProperties = await session.TryGetMediaPropertiesAsync();

        var title = mediaProperties.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var artist = FirstNonEmpty(
            mediaProperties.Artist,
            mediaProperties.AlbumArtist);

        return new PlaybackTrack(
            title,
            artist ?? string.Empty,
            session.SourceAppUserModelId ?? string.Empty);
    }

    private bool IsAllowedSourceApp(string? sourceAppUserModelId)
    {
        if (_allowedSourceApps.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
        {
            return false;
        }

        foreach (var allowedSourceApp in _allowedSourceApps)
        {
            if (sourceAppUserModelId.Contains(allowedSourceApp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
