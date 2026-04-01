namespace LolStatusMusicBridge;

internal sealed class MusicStatusBridge : IDisposable
{
    private readonly AppOptions _options;
    private readonly MusicSessionReader _musicSessionReader;
    private readonly LeagueClientStatusService _leagueClientStatusService;
    private readonly StatusRestoreStateStore _stateStore;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private string? _lastAppliedStatusMessage;
    private string? _initialStatusMessage;
    private string? _lastTrackText;
    private bool _capturedInitialStatus;
    private bool _loggedLeagueReady;
    private bool _recoveryAttempted;
    private bool _waitingForLeagueClient;

    public MusicStatusBridge(
        AppOptions options,
        MusicSessionReader musicSessionReader,
        LeagueClientStatusService leagueClientStatusService,
        StatusRestoreStateStore stateStore)
    {
        _options = options;
        _musicSessionReader = musicSessionReader;
        _leagueClientStatusService = leagueClientStatusService;
        _stateStore = stateStore;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _operationGate.WaitAsync(cancellationToken);

                    try
                    {
                        await SyncOnceAsync(cancellationToken);
                    }
                    finally
                    {
                        _operationGate.Release();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    WriteLog($"Unexpected error: {exception.Message}");
                }

                await Task.Delay(_options.PollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await RestoreStatusIfNeededAsync(CancellationToken.None, "Application closed");
        }
    }

    public void RestoreStatusForShutdown(TimeSpan timeout)
    {
        if (!_operationGate.Wait(timeout))
        {
            WriteLog("Timed out while waiting to restore the previous LoL status during shutdown.");
            return;
        }

        try
        {
            RestoreStatusIfNeededAsync(CancellationToken.None, "Application closed").GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            WriteLog($"Failed to restore the previous LoL status during shutdown: {exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        _operationGate.Dispose();
        _leagueClientStatusService.Dispose();
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        var currentProfile = await _leagueClientStatusService.TryGetSelfAsync(cancellationToken);
        if (currentProfile is null)
        {
            if (!_waitingForLeagueClient)
            {
                var reason = _leagueClientStatusService.LastError ?? "Unknown error.";
                var lockfilePath = _leagueClientStatusService.LastLockfilePath;

                if (string.IsNullOrWhiteSpace(lockfilePath))
                {
                    WriteLog($"League client not ready yet. {reason}");
                }
                else
                {
                    WriteLog($"League client not ready yet. {reason} Lockfile: {lockfilePath}");
                }

                _waitingForLeagueClient = true;
            }

            return;
        }

        _waitingForLeagueClient = false;
        if (!_loggedLeagueReady)
        {
            var lockfilePath = _leagueClientStatusService.LastLockfilePath;
            if (string.IsNullOrWhiteSpace(lockfilePath))
            {
                WriteLog("Connected to League client.");
            }
            else
            {
                WriteLog($"Connected to League client. Lockfile: {lockfilePath}");
            }

            _loggedLeagueReady = true;
        }

        if (!_recoveryAttempted)
        {
            currentProfile = await TryRecoverPreviousSessionAsync(currentProfile, cancellationToken);
            _recoveryAttempted = true;
        }

        if (!_capturedInitialStatus)
        {
            _initialStatusMessage = currentProfile.StatusMessage;
            _capturedInitialStatus = true;
        }

        var track = await _musicSessionReader.TryGetCurrentTrackAsync();
        if (track is null)
        {
            _lastTrackText = null;
            await RestoreStatusIfNeededAsync(cancellationToken, "Music stopped");
            return;
        }

        var desiredStatusMessage = BuildStatusMessage(track);
        if (_lastTrackText != track.DisplayText)
        {
            WriteLog($"Detected track: {track.DisplayText}");
            _lastTrackText = track.DisplayText;
        }

        if (string.Equals(currentProfile.StatusMessage, desiredStatusMessage, StringComparison.Ordinal))
        {
            _lastAppliedStatusMessage = desiredStatusMessage;
            return;
        }

        var updated = await _leagueClientStatusService.TrySetStatusMessageAsync(
            currentProfile,
            desiredStatusMessage,
            cancellationToken);

        if (updated)
        {
            _lastAppliedStatusMessage = desiredStatusMessage;
            PersistRestoreState(desiredStatusMessage);
            WriteLog($"Updated LoL status: {desiredStatusMessage}");
        }
    }

    private async Task RestoreStatusIfNeededAsync(CancellationToken cancellationToken, string reason)
    {
        if (_lastAppliedStatusMessage is null)
        {
            return;
        }

        var currentProfile = await _leagueClientStatusService.TryGetSelfAsync(cancellationToken);
        if (currentProfile is not null &&
            (string.Equals(currentProfile.StatusMessage, _lastAppliedStatusMessage, StringComparison.Ordinal) ||
             string.Equals(reason, "Application closed", StringComparison.Ordinal)))
        {
            var restoreValue = _initialStatusMessage ?? string.Empty;
            var restored = await _leagueClientStatusService.TrySetStatusMessageAsync(
                currentProfile,
                restoreValue,
                cancellationToken);

            if (restored)
            {
                if (string.IsNullOrWhiteSpace(restoreValue))
                {
                    WriteLog($"{reason}. Cleared LoL status.");
                }
                else
                {
                    WriteLog($"{reason}. Restored LoL status: {restoreValue}");
                }

                _stateStore.Clear();
            }
        }

        _lastAppliedStatusMessage = null;
    }

    private void PersistRestoreState(string lastAppliedStatusMessage)
    {
        if (!_capturedInitialStatus)
        {
            return;
        }

        _stateStore.Save(
            new StatusRestoreState(
                _initialStatusMessage ?? string.Empty,
                lastAppliedStatusMessage));
    }

    private async Task<LeagueStatusSnapshot> TryRecoverPreviousSessionAsync(
        LeagueStatusSnapshot currentProfile,
        CancellationToken cancellationToken)
    {
        var pendingRestore = _stateStore.Load();
        if (pendingRestore is null)
        {
            return currentProfile;
        }

        if (!string.Equals(
                currentProfile.StatusMessage,
                pendingRestore.LastAppliedStatusMessage,
                StringComparison.Ordinal))
        {
            WriteLog("Found saved restore state, but the current LoL status no longer matches the previous app status. Skipping recovery.");
            _stateStore.Clear();
            return currentProfile;
        }

        var restored = await _leagueClientStatusService.TrySetStatusMessageAsync(
            currentProfile,
            pendingRestore.InitialStatusMessage,
            cancellationToken);

        if (!restored)
        {
            WriteLog("Found saved restore state, but could not restore the previous LoL status yet.");
            return currentProfile;
        }

        _stateStore.Clear();
        WriteLog("Recovered the previous LoL status from the last session.");

        var refreshedProfile = await _leagueClientStatusService.TryGetSelfAsync(cancellationToken);
        return refreshedProfile ?? new LeagueStatusSnapshot(
            currentProfile.Payload,
            pendingRestore.InitialStatusMessage);
    }

    private string BuildStatusMessage(PlaybackTrack track)
    {
        var baseMessage = string.IsNullOrWhiteSpace(_options.StatusPrefix)
            ? track.DisplayText
            : $"{_options.StatusPrefix}: {track.DisplayText}";

        if (baseMessage.Length <= _options.MaxStatusLength)
        {
            return baseMessage;
        }

        return baseMessage[.._options.MaxStatusLength];
    }

    private static void WriteLog(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
