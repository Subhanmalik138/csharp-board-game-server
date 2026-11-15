namespace GameHub.Game;

/// <summary>
/// The two sides in a game of Connect Four.
/// </summary>
public enum Disc
{
    None = 0,
    Red = 1,
    Yellow = 2
}

/// <summary>
/// Terminal state of a game.
/// </summary>
public enum GameResult
{
    Ongoing,
    RedWins,
    YellowWins,
    Draw
}

/// <summary>
/// Pure Connect Four rules engine: no I/O, no networking, fully deterministic.
/// The board is 7 columns x 6 rows; row 0 is the bottom row, so a dropped disc
/// lands on the lowest empty cell of its column.
/// </summary>
public sealed class ConnectFour
{
    public const int Columns = 7;
    public const int Rows = 6;
    public const int WinLength = 4;

    // cells[column, row]
    private readonly Disc[,] _cells = new Disc[Columns, Rows];

    public Disc CurrentPlayer { get; private set; } = Disc.Red;
    public int MoveCount { get; private set; }
    public GameResult Result { get; private set; } = GameResult.Ongoing;

    /// <summary>Disc in a cell, or <see cref="Disc.None"/> when empty.</summary>
    public Disc Cell(int column, int row) => _cells[column, row];

    /// <summary>True when the top cell of the column is occupied.</summary>
    public bool IsColumnFull(int column) => _cells[column, Rows - 1] != Disc.None;

    public bool IsBoardFull => MoveCount >= Columns * Rows;

    /// <summary>
    /// Drops the current player's disc into <paramref name="column"/> (0-based).
    /// Returns the row the disc landed on, or -1 when the move is illegal
    /// (out of range, column full, or the game is already over).
    /// </summary>
    public int Drop(int column)
    {
        if (Result != GameResult.Ongoing)
            return -1;
        if (column < 0 || column >= Columns || IsColumnFull(column))
            return -1;

        var row = 0;
        while (_cells[column, row] != Disc.None)
            row++;

        _cells[column, row] = CurrentPlayer;
        MoveCount++;

        if (HasFourInARow(column, row))
            Result = CurrentPlayer == Disc.Red ? GameResult.RedWins : GameResult.YellowWins;
        else if (IsBoardFull)
            Result = GameResult.Draw;
        else
            CurrentPlayer = CurrentPlayer == Disc.Red ? Disc.Yellow : Disc.Red;

        return row;
    }

    /// <summary>Columns that still accept a disc.</summary>
    public IEnumerable<int> LegalMoves()
    {
        for (var c = 0; c < Columns; c++)
            if (!IsColumnFull(c))
                yield return c;
    }

    private bool HasFourInARow(int column, int row)
    {
        var disc = _cells[column, row];
        // Only the four directions that can pass through the last move matter.
        var directions = new (int dc, int dr)[] { (1, 0), (0, 1), (1, 1), (1, -1) };
        foreach (var (dc, dr) in directions)
        {
            var run = 1 + CountInDirection(column, row, dc, dr, disc)
                        + CountInDirection(column, row, -dc, -dr, disc);
            if (run >= WinLength)
                return true;
        }
        return false;
    }

    private int CountInDirection(int column, int row, int dc, int dr, Disc disc)
    {
        var count = 0;
        var c = column + dc;
        var r = row + dr;
        while (c >= 0 && c < Columns && r >= 0 && r < Rows && _cells[c, r] == disc)
        {
            count++;
            c += dc;
            r += dr;
        }
        return count;
    }

    /// <summary>Human-readable board, top row first, for the CLI client.</summary>
    public string Render()
    {
        var lines = new List<string>();
        for (var r = Rows - 1; r >= 0; r--)
        {
            var cells = new char[Columns];
            for (var c = 0; c < Columns; c++)
                cells[c] = _cells[c, r] switch
                {
                    Disc.Red => 'R',
                    Disc.Yellow => 'Y',
                    _ => '.'
                };
            lines.Add("| " + string.Join(" ", cells) + " |");
        }
        lines.Add("  " + string.Join(" ", Enumerable.Range(1, Columns)));
        return string.Join(Environment.NewLine, lines);
    }
}
