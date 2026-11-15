using System.Net;
using System.Net.Sockets;

namespace GameHub.Server;

/// <summary>
/// The TCP front door: listens on a port and hands each connection to a
/// <see cref="ClientSession"/> on its own task. The lobby is the only shared
/// state, and it is internally synchronized, so the accept loop stays trivial.
/// </summary>
public sealed class GameServer
{
    private readonly Lobby _lobby = new();
    private readonly int _port;
    private TcpListener? _listener;

    public GameServer(int port = 5000) => _port = port;

    public Lobby Lobby => _lobby;

    public async Task RunAsync(CancellationToken token = default)
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        Console.WriteLine($"GameHub listening on port {_port}. Press Ctrl+C to stop.");

        try
        {
            while (!token.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(token);
                _ = HandleClientAsync(tcp, token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken token)
    {
        var session = new ClientSession(tcp, _lobby);
        var endpoint = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
        Console.WriteLine($"[+] {endpoint} connected");
        await session.RunAsync(token);
        Console.WriteLine($"[-] {session.Name} ({endpoint}) disconnected");
    }
}
