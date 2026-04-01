using LolStatusMusicBridge;
using System.Threading;

var options = AppOptions.Load();
using var bridge = new MusicStatusBridge(
    options,
    new MusicSessionReader(options),
    new LeagueClientStatusService(new LeagueClientLocator(), options.LeagueLockfilePath));
using var shutdown = new CancellationTokenSource();
var shutdownStarted = 0;
using var shutdownMonitor = ConsoleShutdownMonitor.Register(ShutdownBridge);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    ShutdownBridge();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    ShutdownBridge();
};

Console.WriteLine("LolStatusMusicBridge started.");
Console.WriteLine($"Config file: {options.ConfigPath}");
Console.WriteLine($"Allowed media apps: {string.Join(", ", options.AllowedSourceApps)}");
Console.WriteLine($"Polling every {options.PollInterval.TotalSeconds:0} seconds.");
Console.WriteLine("Press Ctrl+C to stop.");

await bridge.RunAsync(shutdown.Token);

ShutdownBridge();

void ShutdownBridge()
{
    if (Interlocked.Exchange(ref shutdownStarted, 1) != 0)
    {
        return;
    }

    shutdown.Cancel();
    bridge.RestoreStatusForShutdown(TimeSpan.FromSeconds(8));
}
