using GameHub.Protocol;
using GameHub.Server;

namespace GameHub.Tests;

/// <summary>In-memory stand-in for a TCP client: captures everything sent to it.</summary>
public sealed class FakeConnection : IClientConnection
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; }
    public List<string> Sent { get; } = new();
    public bool Closed { get; private set; }

    public FakeConnection(string name) => Name = name;

    public void Send(string json) => Sent.Add(json);
    public void Close() => Closed = true;

    public List<string> MessagesOfType(string type) =>
        Sent.Where(json =>
            Wire.TryParse(json, out var t, out _) && t == type).ToList();

    public bool Received(string type) => MessagesOfType(type).Count > 0;
}

public sealed class LobbyTests
{
    [Fact]
    public void Enqueue_PairsTwoWaitingPlayersIntoARoom()
    {
        var lobby = new Lobby();
        var alice = new FakeConnection("alice");
        var bob = new FakeConnection("bob");

        Assert.Null(lobby.Enqueue(alice));
        Assert.Equal(1, lobby.QueuedCount);

        var room = lobby.Enqueue(bob);
        Assert.NotNull(room);
        Assert.Equal(0, lobby.QueuedCount);
        Assert.Single(lobby.Rooms);

        // Both players learn the match started and see the opening board.
        Assert.True(alice.Received(Wire.GameStart));
        Assert.True(bob.Received(Wire.GameStart));
        Assert.True(alice.Received(Wire.Board));
        Assert.True(bob.Received(Wire.Board));
    }

    [Fact]
    public void Enqueue_SameClientTwice_DoesNotDuplicate()
    {
        var lobby = new Lobby();
        var alice = new FakeConnection("alice");
        lobby.Enqueue(alice);
        lobby.Enqueue(alice);
        Assert.Equal(1, lobby.QueuedCount);
    }

    [Fact]
    public void QueuePosition_ReflectsWaitingState()
    {
        var lobby = new Lobby();
        var first = new FakeConnection("first");
        var second = new FakeConnection("second");
        var third = new FakeConnection("third");

        lobby.Enqueue(first);
        Assert.Equal(1, lobby.QueuePosition(first));

        // The second arrival pairs immediately, so both leave the queue.
        lobby.Enqueue(second);
        Assert.Equal(0, lobby.QueuePosition(first));
        Assert.Equal(0, lobby.QueuePosition(second));
        Assert.Single(lobby.Rooms);

        lobby.Enqueue(third);
        Assert.Equal(1, lobby.QueuePosition(third));
    }

    [Fact]
    public void LeaveQueue_RemovesClient()
    {
        var lobby = new Lobby();
        var alice = new FakeConnection("alice");
        lobby.Enqueue(alice);
        lobby.LeaveQueue(alice);
        Assert.Equal(0, lobby.QueuedCount);
        Assert.Equal(0, lobby.QueuePosition(alice));
    }

    [Fact]
    public void RemoveClient_DropsRoom_WhenPlayerDisconnects()
    {
        var lobby = new Lobby();
        var alice = new FakeConnection("alice");
        var bob = new FakeConnection("bob");
        lobby.Enqueue(alice);
        _ = lobby.Enqueue(bob);

        lobby.RemoveClient(alice);
        Assert.Empty(lobby.Rooms);
        // The opponent is told they won by forfeit.
        Assert.Contains(bob.Sent, json => json.Contains("forfeit"));
    }

    [Fact]
    public void ListRooms_ReportsOpenGames()
    {
        var lobby = new Lobby();
        var alice = new FakeConnection("alice");
        var bob = new FakeConnection("bob");
        lobby.Enqueue(alice);
        var room = lobby.Enqueue(bob)!;

        var rooms = lobby.ListRooms();
        Assert.Single(rooms);
        Assert.Equal(room.Id, rooms[0]["id"]);
        Assert.Equal("alice", rooms[0]["red"]);
        Assert.Equal("bob", rooms[0]["yellow"]);
        Assert.Equal(0, rooms[0]["spectators"]);
    }
}

public sealed class RoomTests
{
    private static (Room Room, FakeConnection Alice, FakeConnection Bob) NewRoom()
    {
        var alice = new FakeConnection("alice");
        var bob = new FakeConnection("bob");
        var room = new Room(alice, bob);
        room.Start();
        return (room, alice, bob);
    }

    [Fact]
    public void TryMove_RejectsOutOfTurn_AndAcceptsTurn()
    {
        var (room, alice, bob) = NewRoom();
        Assert.NotNull(room.TryMove(bob, 0));   // bob goes second
        Assert.Null(room.TryMove(alice, 3));    // alice opens
        Assert.Null(room.TryMove(bob, 4));
    }

    [Fact]
    public void TryMove_BroadcastsBoardToBothPlayers()
    {
        var (room, alice, bob) = NewRoom();
        var boardsBefore = alice.MessagesOfType(Wire.Board).Count;
        room.TryMove(alice, 3);
        Assert.Equal(boardsBefore + 1, alice.MessagesOfType(Wire.Board).Count);
        Assert.True(bob.Received(Wire.Board));
    }

    [Fact]
    public void TryMove_AfterWin_RejectsFurtherMoves()
    {
        var (room, alice, bob) = NewRoom();
        foreach (var column in new[] { 0, 6, 1, 6, 2, 6, 3 })
        {
            var player = room.Session.CurrentPlayerName == "alice" ? alice : bob;
            Assert.Null(room.TryMove(player, column));
        }
        Assert.True(room.IsOver);
        Assert.NotNull(room.TryMove(bob, 0));
    }

    [Fact]
    public void Chat_ReachesEveryoneInTheRoom()
    {
        var (room, alice, bob) = NewRoom();
        room.Chat(alice, "good luck!");
        Assert.Contains(bob.MessagesOfType(Wire.ChatMessage), m => m.Contains("good luck!"));
        Assert.Contains(alice.MessagesOfType(Wire.ChatMessage), m => m.Contains("good luck!"));
    }

    [Fact]
    public void Spectator_CanWatchButCannotMove()
    {
        var (room, alice, bob) = NewRoom();
        var carol = new FakeConnection("carol");
        room.AddSpectator(carol);
        Assert.Equal(1, room.SpectatorCount);
        Assert.True(carol.Received(Wire.Board));

        Assert.NotNull(room.TryMove(carol, 0));
        Assert.Equal("alice", room.Session.CurrentPlayerName); // no move happened

        room.RemoveSpectator(carol);
        Assert.Equal(0, room.SpectatorCount);
    }

    [Fact]
    public void Rematch_NeedsBothPlayers_AndSwapsColours()
    {
        var (room, alice, bob) = NewRoom();
        foreach (var column in new[] { 0, 6, 1, 6, 2, 6, 3 })
        {
            var player = room.Session.CurrentPlayerName == "alice" ? alice : bob;
            room.TryMove(player, column);
        }
        Assert.True(room.IsOver);

        room.RequestRematch(alice);
        Assert.True(room.IsOver); // one vote is not enough: same finished game
        Assert.Equal("alice", room.Session.RedPlayer);

        room.RequestRematch(bob);
        Assert.False(room.Session.IsOver);
        Assert.Equal("bob", room.Session.RedPlayer); // colours swapped
        Assert.Equal("bob", room.Session.CurrentPlayerName);
        Assert.Equal(0, room.Session.Game.MoveCount);

        // Board labels follow the swap: bob is told he is Red now.
        var lastBoard = bob.MessagesOfType(Wire.Board).Last();
        Assert.Contains("\"you_are\":\"R\"", lastBoard);
        var aliceBoard = alice.MessagesOfType(Wire.Board).Last();
        Assert.Contains("\"you_are\":\"Y\"", aliceBoard);
    }

    [Fact]
    public void PlayerDisconnected_ClosesRoom()
    {
        var (room, alice, bob) = NewRoom();
        var closed = false;
        room.Closed += _ => closed = true;
        room.PlayerDisconnected(alice);
        Assert.True(closed);
    }

    [Fact]
    public void SpectatorDisconnected_DoesNotCloseRoom()
    {
        var (room, _, _) = NewRoom();
        var carol = new FakeConnection("carol");
        room.AddSpectator(carol);
        var closed = false;
        room.Closed += _ => closed = true;
        room.PlayerDisconnected(carol);
        Assert.False(closed);
        Assert.Equal(0, room.SpectatorCount);
    }
}
