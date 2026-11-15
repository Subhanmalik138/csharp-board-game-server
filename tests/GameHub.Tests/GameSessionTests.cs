using GameHub.Game;

namespace GameHub.Tests;

public sealed class GameSessionTests
{
    [Fact]
    public void Play_EnforcesTurnsByName()
    {
        var session = new GameSession("alice", "bob");
        Assert.Equal("alice", session.CurrentPlayerName);

        Assert.Equal(-1, session.Play("bob", 0));   // not bob's turn
        Assert.Equal(0, session.Play("alice", 0));  // alice drops
        Assert.Equal("bob", session.CurrentPlayerName);
        Assert.Equal(-1, session.Play("alice", 1)); // not alice's turn now
        Assert.Equal(-1, session.Play("mallory", 1)); // unknown player
    }

    [Fact]
    public void Play_TracksWinnerByName()
    {
        var session = new GameSession("alice", "bob");
        foreach (var column in new[] { 0, 6, 1, 6, 2, 6, 3 })
            Assert.NotEqual(-1, session.Play(session.CurrentPlayerName, column));
        Assert.True(session.IsOver);
        Assert.Equal("alice", session.WinnerName);
        Assert.Equal(-1, session.Play("alice", 4)); // game over
    }

    [Fact]
    public void Rematch_SwapsColours_AndResetsBoard()
    {
        var session = new GameSession("alice", "bob");
        foreach (var column in new[] { 0, 6, 1, 6, 2, 6, 3 })
            session.Play(session.CurrentPlayerName, column);
        Assert.True(session.IsOver);

        session.RequestRematch("alice");
        Assert.False(session.BothWantRematch);
        session.RequestRematch("bob");
        Assert.True(session.BothWantRematch);

        session.StartRematch();
        Assert.Equal("bob", session.RedPlayer);   // bob opens the next game
        Assert.Equal("alice", session.YellowPlayer);
        Assert.Equal("bob", session.CurrentPlayerName);
        Assert.False(session.IsOver);
        Assert.Equal(0, session.Game.MoveCount);
        Assert.Empty(session.Moves);
        Assert.Null(session.WinnerName);
    }

    [Fact]
    public void RequestRematch_BeforeGameOver_DoesNothing()
    {
        var session = new GameSession("alice", "bob");
        session.RequestRematch("alice");
        Assert.False(session.BothWantRematch);
    }

    [Fact]
    public void Moves_AreRecordedInOrder()
    {
        var session = new GameSession("alice", "bob");
        session.Play("alice", 3);
        session.Play("bob", 4);
        Assert.Equal(new[] { ("alice", 3, 0), ("bob", 4, 0) }, session.Moves);
    }
}
