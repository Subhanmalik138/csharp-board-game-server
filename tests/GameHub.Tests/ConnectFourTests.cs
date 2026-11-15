using GameHub.Game;

namespace GameHub.Tests;

public sealed class ConnectFourTests
{
    [Fact]
    public void Drop_LandsOnLowestEmptyCell_AndAlternatesTurns()
    {
        var game = new ConnectFour();
        Assert.Equal(0, game.Drop(3));
        Assert.Equal(Disc.Yellow, game.CurrentPlayer);
        Assert.Equal(1, game.Drop(3));
        Assert.Equal(Disc.Red, game.CurrentPlayer);
        Assert.Equal(Disc.Red, game.Cell(3, 0));
        Assert.Equal(Disc.Yellow, game.Cell(3, 1));
        Assert.Equal(2, game.MoveCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Drop_RejectsOutOfRangeColumns(int column)
    {
        var game = new ConnectFour();
        Assert.Equal(-1, game.Drop(column));
        Assert.Equal(0, game.MoveCount);
    }

    [Fact]
    public void Drop_RejectsFullColumn()
    {
        var game = new ConnectFour();
        for (var i = 0; i < ConnectFour.Rows; i++)
            Assert.NotEqual(-1, game.Drop(0));
        Assert.True(game.IsColumnFull(0));
        Assert.Equal(-1, game.Drop(0));
        Assert.Equal(ConnectFour.Rows, game.MoveCount);
    }

    [Fact]
    public void HorizontalWin_Detected()
    {
        // Red takes columns 1-4 on the bottom row; Yellow wastes moves in 6-7.
        var game = Play(0, 6, 1, 6, 2, 6, 3);
        Assert.Equal(GameResult.RedWins, game.Result);
        Assert.Equal(-1, game.Drop(4)); // no moves after the game ends
    }

    [Fact]
    public void VerticalWin_Detected()
    {
        var game = Play(3, 0, 3, 1, 3, 2, 3);
        Assert.Equal(GameResult.RedWins, game.Result);
    }

    [Fact]
    public void DiagonalWin_Rising_Detected()
    {
        // Build a rising diagonal for Red: (0,0) (1,1) (2,2) (3,3).
        // Yellow pads underneath so Red's discs land at the right heights.
        var game = Play(
            0, // R (0,0)
            1, // Y (1,0)
            1, // R (1,1)
            2, // Y (2,0)
            6, // R filler
            2, // Y (2,1)
            2, // R (2,2)
            3, // Y (3,0)
            6, // R filler
            3, // Y (3,1)
            6, // R filler
            3, // Y (3,2)
            3  // R (3,3) — wins
        );
        Assert.Equal(GameResult.RedWins, game.Result);
    }

    [Fact]
    public void DiagonalWin_Falling_Detected()
    {
        // Falling diagonal for Red: (3,0) (2,1) (1,2) (0,3).
        var game = Play(
            3, // R (3,0)
            2, // Y (2,0)
            2, // R (2,1)
            1, // Y (1,0)
            6, // R filler
            1, // Y (1,1)
            1, // R (1,2)
            0, // Y (0,0)
            6, // R filler
            0, // Y (0,1)
            6, // R filler
            0, // Y (0,2)
            0  // R (0,3) — wins
        );
        Assert.Equal(GameResult.RedWins, game.Result);
    }

    [Fact]
    public void FullBoard_WithoutFourInARow_IsDraw()
    {
        // 42-move sequence found by random playout that never completes four.
        var game = Play(
            5, 5, 5, 4, 4, 1, 4, 0, 0, 3, 2, 5, 2, 0,
            0, 4, 5, 0, 3, 0, 4, 3, 3, 1, 1, 4, 6, 3,
            1, 2, 5, 3, 1, 6, 6, 1, 6, 2, 6, 2, 2, 6);
        Assert.Equal(42, game.MoveCount);
        Assert.True(game.IsBoardFull);
        Assert.Equal(GameResult.Draw, game.Result);
    }

    [Fact]
    public void LegalMoves_ShrinksAsColumnsFill()
    {
        var game = new ConnectFour();
        Assert.Equal(7, game.LegalMoves().Count());
        for (var i = 0; i < ConnectFour.Rows; i++)
            game.Drop(0);
        Assert.DoesNotContain(0, game.LegalMoves());
        Assert.Equal(6, game.LegalMoves().Count());
    }

    [Fact]
    public void Render_ShowsBoardWithColumnNumbers()
    {
        var game = Play(3, 0);
        var rendered = game.Render();
        Assert.Contains("R", rendered);
        Assert.Contains("Y", rendered);
        Assert.Contains("1 2 3 4 5 6 7", rendered);
    }

    /// <summary>Plays columns in order, alternating Red/Yellow, starting with Red.</summary>
    private static ConnectFour Play(params int[] columns)
    {
        var game = new ConnectFour();
        foreach (var column in columns)
        {
            var row = game.Drop(column);
            Assert.NotEqual(-1, row);
        }
        return game;
    }
}
