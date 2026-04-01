using System.Runtime.InteropServices;

namespace LolStatusMusicBridge;

internal sealed class ConsoleShutdownMonitor : IDisposable
{
    private readonly HandlerRoutine _handler;
    private readonly Action _onShutdown;
    private readonly bool _registered;
    private bool _disposed;

    private ConsoleShutdownMonitor(Action onShutdown)
    {
        _onShutdown = onShutdown;
        _handler = HandleShutdownSignal;
        _registered = SetConsoleCtrlHandler(_handler, true);
    }

    public static ConsoleShutdownMonitor Register(Action onShutdown)
    {
        return new ConsoleShutdownMonitor(onShutdown);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_registered)
        {
            SetConsoleCtrlHandler(_handler, false);
        }
    }

    private bool HandleShutdownSignal(ConsoleControlSignal signal)
    {
        switch (signal)
        {
            case ConsoleControlSignal.Close:
            case ConsoleControlSignal.Logoff:
            case ConsoleControlSignal.Shutdown:
            case ConsoleControlSignal.Break:
                _onShutdown();
                break;
        }

        return false;
    }

    [DllImport("Kernel32")]
    private static extern bool SetConsoleCtrlHandler(HandlerRoutine? handler, bool add);

    private delegate bool HandlerRoutine(ConsoleControlSignal signal);

    private enum ConsoleControlSignal : uint
    {
        CtrlC = 0,
        Break = 1,
        Close = 2,
        Logoff = 5,
        Shutdown = 6
    }
}
