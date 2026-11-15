namespace GameHub.Game;

/// <summary>
/// One match between two named players: owns the engine, tracks whose turn it
/// is by name, and keeps a short move history so clients can replay a game.
/// A rematch starts a fresh engine with the colours swapped, so the player who
/// went second gets the opening move next time.
/// </summary>
public sealed class GameSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string RedPlayer { get; private set; }
    public string YellowPlayer { get; private set; }
    public ConnectFour Game { get; private set; } = new();
    public List<(string Player, int Column, int Row)> Moves { get; } = new();
    public bool RematchRequestedByRed { get; private set; }
    public bool RematchRequestedByYellow { get; private set; }

    public GameSession(string redPlayer, string yellowPlayer)
    {
        RedPlayer = redPlayer;
        YellowPlayer = yellowPlayer;
    }

    public string CurrentPlayerName =>
        Game.CurrentPlayer == Disc.Red ? RedPlayer : YellowPlayer;

    public bool IsOver => Game.Result != GameResult.Ongoing;

    public string? WinnerName => Game.Result switch
    {
        GameResult.RedWins => RedPlayer,
        GameResult.YellowWins => YellowPlayer,
        _ => null
    };

    /// <summary>
    /// Applies a move for <paramref name="playerName"/>. Returns the row landed
    /// on, or -1 for an illegal move (wrong turn, bad column, game over).
    /// </summary>
    public int Play(string playerName, int column)
    {
        if (IsOver || playerName != CurrentPlayerName)
            return -1;

        var row = Game.Drop(column);
        if (row >= 0)
            Moves.Add((playerName, column, row));
        return row;
    }

    public void RequestRematch(string playerName)
    {
        if (playerName == RedPlayer) RematchRequestedByRed = true;
        if (playerName == YellowPlayer) RematchRequestedByYellow = true;
    }

    public bool BothWantRematch => RematchRequestedByRed && RematchRequestedByYellow;

    /// <summary>Starts a new game with the colours swapped.</summary>
    public void StartRematch()
    {
        (RedPlayer, YellowPlayer) = (YellowPlayer, RedPlayer);
        Game = new ConnectFour();
        Moves.Clear();
        RematchRequestedByRed = false;
        RematchRequestedByYellow = false;
    }
}
