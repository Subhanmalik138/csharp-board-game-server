using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GameHub.Protocol;

namespace GameHub.Server;

/// <summary>
/// One TCP connection. Owns the socket, the read loop, and the translation
/// between wire messages and lobby/room calls. All sends are best-effort: a
/// dead socket must never take the server down with it.
/// </summary>
public sealed class ClientSession : IClientConnection
{
    private readonly TcpClient _tcp;
    private readonly Lobby _lobby;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly object _writeLock = new();
    private bool _closed;

    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "guest";

    public ClientSession(TcpClient tcp, Lobby lobby)
    {
        _tcp = tcp;
        _lobby = lobby;
        var stream = tcp.GetStream();
        // UTF-8 without BOM: a BOM prefix would break strict JSON parsers
        // in clients reading our first (welcome) message.
        var noBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        _reader = new StreamReader(stream, noBom);
        _writer = new StreamWriter(stream, noBom) { AutoFlush = true };
    }

    public void Send(string json)
    {
        lock (_writeLock)
        {
            if (_closed) return;
            try { _writer.WriteLine(json); }
            catch (IOException) { /* peer gone; read loop will clean up */ }
            catch (ObjectDisposedException) { }
        }
    }

    public void Close()
    {
        lock (_writeLock)
        {
            if (_closed) return;
            _closed = true;
        }
        try { _tcp.Close(); } catch { }
    }

    /// <summary>Runs until the client disconnects; then removes them from the lobby.</summary>
    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            Send(Wire.Encode(Wire.Welcome, new()
            {
                ["name"] = "GameHub",
                ["motd"] = "Welcome to GameHub! Send {\"type\":\"hello\",\"name\":\"you\"}, then {\"type\":\"queue\"} to play."
            }));

            while (!token.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(token);
                if (line is null) break; // clean disconnect
                Handle(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { /* dropped connection */ }
        catch (ObjectDisposedException) { }
        finally
        {
            _lobby.RemoveClient(this);
            Close();
        }
    }

    private void Handle(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (!Wire.TryParse(line, out var type, out var data))
        {
            SendError("That is not a valid JSON message.");
            return;
        }

        switch (type)
        {
            case Wire.Hello:
                Name = SanitizeName(Wire.GetString(data, "name", Name));
                Send(Wire.Encode(Wire.Info, new() { ["message"] = $"Hi {Name}! Type 'queue' to find a game, 'rooms' to spectate." }));
                break;

            case Wire.Queue:
                var room = _lobby.Enqueue(this);
                if (room is null)
                    Send(Wire.Encode(Wire.Queued, new() { ["position"] = _lobby.QueuePosition(this) }));
                break;

            case Wire.LeaveQueue:
                _lobby.LeaveQueue(this);
                Send(Wire.Encode(Wire.Info, new() { ["message"] = "Left the queue." }));
                break;

            case Wire.Rooms:
                Send(Wire.Encode(Wire.RoomList, new() { ["rooms"] = _lobby.ListRooms() }));
                break;

            case Wire.Spectate:
            {
                var target = _lobby.FindRoom(Wire.GetString(data, "room"));
                if (target is null)
                    SendError("No room with that id. Ask for 'rooms' first.");
                else if (target.IsPlayer(this))
                    SendError("You are playing in that room already.");
                else
                    target.AddSpectator(this);
                break;
            }

            case Wire.Move:
            {
                var current = _lobby.FindRoomOf(this);
                if (current is null)
                    SendError("You are not in a game. Join the queue first.");
                else
                {
                    var error = current.TryMove(this, Wire.GetInt(data, "column"));
                    if (error is not null) SendError(error);
                }
                break;
            }

            case Wire.Chat:
            {
                var current = _lobby.FindRoomOf(this);
                if (current is null)
                    SendError("Join a game or spectate a room before chatting.");
                else
                    current.Chat(this, Wire.GetString(data, "text"));
                break;
            }

            case Wire.Rematch:
            {
                var current = _lobby.FindRoomOf(this);
                if (current is null)
                    SendError("You are not in a game.");
                else
                    current.RequestRematch(this);
                break;
            }

            case Wire.Leave:
                _lobby.RemoveClient(this);
                Send(Wire.Encode(Wire.Info, new() { ["message"] = "You left the room." }));
                break;

            default:
                SendError($"Unknown message type '{type}'.");
                break;
        }
    }

    private void SendError(string message) =>
        Send(Wire.Encode(Wire.Error, new() { ["message"] = message }));

    internal static string SanitizeName(string name)
    {
        var clean = new string(name.Trim().Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (clean.Length == 0) clean = "guest";
        if (clean.Length > 20) clean = clean[..20];
        return clean;
    }
}
