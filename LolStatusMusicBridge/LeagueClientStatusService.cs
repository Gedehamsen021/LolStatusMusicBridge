using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json.Nodes;

namespace LolStatusMusicBridge;

internal sealed class LeagueClientStatusService : IDisposable
{
    private readonly LeagueClientLocator _locator;
    private readonly string? _configuredLockfilePath;
    private HttpClient? _httpClient;
    private LeagueLockfileCredentials? _activeCredentials;
    public string? LastError { get; private set; }
    public string? LastLockfilePath { get; private set; }

    public LeagueClientStatusService(
        LeagueClientLocator locator,
        string? configuredLockfilePath)
    {
        _locator = locator;
        _configuredLockfilePath = configuredLockfilePath;
    }

    public async Task<LeagueStatusSnapshot?> TryGetSelfAsync(CancellationToken cancellationToken)
    {
        var client = EnsureClient();
        if (client is null)
        {
            return null;
        }

        try
        {
            using var response = await client.GetAsync("lol-chat/v1/me", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LastError = $"LCU returned HTTP {(int)response.StatusCode} for /lol-chat/v1/me.";
                ResetClient();
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonNode.Parse(body) as JsonObject;
            if (payload is null)
            {
                LastError = "LCU returned an empty or invalid JSON payload.";
                return null;
            }

            var statusMessage = payload["statusMessage"]?.GetValue<string>() ?? string.Empty;
            LastError = null;
            return new LeagueStatusSnapshot(payload, statusMessage);
        }
        catch (Exception exception)
        {
            LastError = $"LCU request failed: {exception.Message}";
            ResetClient();
            return null;
        }
    }

    public async Task<bool> TrySetStatusMessageAsync(
        LeagueStatusSnapshot snapshot,
        string statusMessage,
        CancellationToken cancellationToken)
    {
        var client = EnsureClient();
        if (client is null)
        {
            return false;
        }

        snapshot.Payload["statusMessage"] = statusMessage;

        try
        {
            using var content = new StringContent(
                snapshot.Payload.ToJsonString(),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PutAsync("lol-chat/v1/me", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ResetClient();
                return false;
            }

            return true;
        }
        catch
        {
            ResetClient();
            return false;
        }
    }

    public void Dispose()
    {
        DisposeClient();
    }

    private HttpClient? EnsureClient()
    {
        var credentials = _locator.TryReadCredentials(_configuredLockfilePath);
        if (credentials is null)
        {
            LastLockfilePath = _configuredLockfilePath;
            LastError = "League lockfile was not found.";
            ResetClient();
            return null;
        }

        LastLockfilePath = credentials.LockfilePath;

        if (_httpClient is not null && credentials == _activeCredentials)
        {
            return _httpClient;
        }

        DisposeClient();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            CheckCertificateRevocationList = false,
            ClientCertificateOptions = ClientCertificateOption.Manual,
            SslProtocols = SslProtocols.Tls12,
            UseProxy = false
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = credentials.BaseUri,
            Timeout = TimeSpan.FromSeconds(5)
        };

        var authToken = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"riot:{credentials.Password}"));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        _httpClient = client;
        _activeCredentials = credentials;
        LastError = null;

        return client;
    }

    private void ResetClient()
    {
        DisposeClient();
        _activeCredentials = null;
    }

    private void DisposeClient()
    {
        _httpClient?.Dispose();
        _httpClient = null;
    }
}
