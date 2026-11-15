using GameHub.Game;
using GameHub.Protocol;

namespace GameHub.Server;

/// <summary>
/// One live match: two players, any number of spectators, and the game
/// session. All state changes go through a single lock, so concurrent moves
/// and chats from different connections serialize cleanly. The room notifies
/// the lobby when it closes (game finished + both players left, or a player
/// disconnected) so it can be dropped from the room list.
/// </summary>
public sealed class Room
{
    private readonly object _lock = new();
    private readonly List<IClientConnection> _spectators = new();

    public GameSession Session { get; }
    public IClientConnection Red { get; }
    public IClientConnection Yellow { get; }
    public string Id => Session.Id;
    public bool IsOver => Session.IsOver;

    public int SpectatorCount
    {
        get { lock (_lock) return _spectators.Count; }
    }

    public Room(IClientConnection red, IClientConnection yellow)
    {
        Red = red;
        Yellow = yellow;
        Session = new GameSession(red.Name, yellow.Name);
    }

    /// <summary>Announces the match to both players and sends the first board.</summary>
    public void Start()
    {
        SendTo(Red, Wire.GameStart, new()
        {
            ["room"] = Id,
            ["red"] = Red.Name,
            ["yellow"] = Yellow.Name,
            ["you_are"] = "R"
        });
        SendTo(Yellow, Wire.GameStart, new()
        {
            ["room"] = Id,
            ["red"] = Red.Name,
            ["yellow"] = Yellow.Name,
            ["you_are"] = "Y"
        });
        BroadcastBoard();
    }

    public void AddSpectator(IClientConnection spectator)
    {
        lock (_lock) _spectators.Add(spectator);
        SendBoardTo(spectator, null);
        Broadcast(Wire.Info, new() { ["message"] = $"{spectator.Name} is now watching." });
    }

    public void RemoveSpectator(IClientConnection spectator)
    {
        lock (_lock) _spectators.Remove(spectator);
    }

    public bool Contains(IClientConnection client) =>
        client == Red || client == Yellow || IsSpectator(client);

    public bool IsSpectator(IClientConnection client)
    {
        lock (_lock) return _spectators.Contains(client);
    }

    public bool IsPlayer(IClientConnection client) => client == Red || client == Yellow;

    /// <summary>
    /// Applies a move. Returns an error message when the move is rejected,
    /// null when it was applied (and the new board broadcast).
    /// </summary>
    public string? TryMove(IClientConnection player, int column)
    {
        lock (_lock)
        {
            if (!IsPlayer(player)) return "Spectators cannot move.";
            if (Session.Play(player.Name, column) < 0)
                return Session.IsOver
                    ? "The game is over. Type 'rematch' to play again."
                    : "Not your turn, or that column is full.";
            BroadcastBoard();
            if (Session.IsOver)
                Broadcast(Wire.Info, new()
                {
                    ["message"] = Session.Game.Result == GameResult.Draw
                        ? "Draw — the board is full."
                        : $"{Session.WinnerName} wins!"
                });
            return null;
        }
    }

    public void Chat(IClientConnection from, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Broadcast(Wire.ChatMessage, new()
        {
            ["from"] = from.Name,
            ["text"] = text.Trim()
        });
    }

    /// <summary>
    /// A rematch starts as soon as both players ask for one; colours swap so
    /// the second player opens the next game.
    /// </summary>
    public void RequestRematch(IClientConnection player)
    {
        lock (_lock)
        {
            if (!IsPlayer(player) || !Session.IsOver) return;
            Session.RequestRematch(player.Name);
            if (Session.BothWantRematch)
            {
                Session.StartRematch();
                Broadcast(Wire.Info, new() { ["message"] = "Rematch! Colours swapped." });
                BroadcastBoard();
            }
            else
            {
                Broadcast(Wire.Info, new()
                {
                    ["message"] = $"{player.Name} wants a rematch. Waiting for the other player."
                });
            }
        }
    }

    /// <summary>
    /// A player leaving mid-game forfeits; the opponent is told and the room
    /// closes. Spectators just stop watching.
    /// </summary>
    public void PlayerDisconnected(IClientConnection player)
    {
        lock (_lock)
        {
            if (!IsPlayer(player))
            {
                _spectators.Remove(player);
                return;
            }
            var other = player == Red ? Yellow : Red;
            if (!Session.IsOver)
            {
                Broadcast(Wire.Info, new()
                {
                    ["message"] = $"{player.Name} disconnected. {other.Name} wins by forfeit."
                });
            }
            Closed?.Invoke(this);
        }
    }

    public event Action<Room>? Closed;

    private void BroadcastBoard()
    {
        SendBoardTo(Red, "R");
        SendBoardTo(Yellow, "Y");
        List<IClientConnection> spectators;
        lock (_lock) spectators = _spectators.ToList();
        foreach (var spectator in spectators)
            SendBoardTo(spectator, null);
    }

    private void SendBoardTo(IClientConnection client, string? youAre)
    {
        var board = Session.Game.Render().Split(Environment.NewLine);
        SendTo(client, Wire.Board, new()
        {
            ["room"] = Id,
            ["board"] = board,
            ["current"] = Session.CurrentPlayerName,
            ["result"] = Session.Game.Result.ToString(),
            ["winner"] = Session.WinnerName,
            ["you_are"] = youAre
        });
    }

    private void Broadcast(string type, Dictionary<string, object?> fields)
    {
        var json = Wire.Encode(type, fields);
        Red.Send(json);
        Yellow.Send(json);
        List<IClientConnection> spectators;
        lock (_lock) spectators = _spectators.ToList();
        foreach (var spectator in spectators)
            spectator.Send(json);
    }

    private static void SendTo(IClientConnection client, string type, Dictionary<string, object?> fields) =>
        client.Send(Wire.Encode(type, fields));
}
