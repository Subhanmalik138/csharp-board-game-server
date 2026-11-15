using GameHub.Protocol;

namespace GameHub.Server;

/// <summary>
/// The lobby: a first-come-first-served matchmaking queue plus the registry
/// of live rooms. Pairing is FIFO — the two players waiting longest play each
/// other — and a room is removed from the list as soon as it closes.
/// </summary>
public sealed class Lobby
{
    private readonly object _lock = new();
    private readonly List<IClientConnection> _queue = new();
    private readonly Dictionary<string, Room> _rooms = new();

    public int QueuedCount
    {
        get { lock (_lock) return _queue.Count; }
    }

    public IReadOnlyCollection<Room> Rooms
    {
        get { lock (_lock) return _rooms.Values.ToList(); }
    }

    /// <summary>
    /// Joins the queue. When a second player is waiting, both are paired into
    /// a new room and the room is returned; otherwise null (still waiting).
    /// </summary>
    public Room? Enqueue(IClientConnection client)
    {
        Room? room = null;
        lock (_lock)
        {
            if (_queue.Contains(client) || FindRoomOf(client) is not null)
                return null;
            _queue.Add(client);
            if (_queue.Count >= 2)
            {
                var red = _queue[0];
                var yellow = _queue[1];
                _queue.RemoveRange(0, 2);
                room = new Room(red, yellow);
                room.Closed += OnRoomClosed;
                _rooms[room.Id] = room;
            }
        }
        room?.Start();
        return room;
    }

    public void LeaveQueue(IClientConnection client)
    {
        lock (_lock) _queue.Remove(client);
    }

    public int QueuePosition(IClientConnection client)
    {
        lock (_lock) return _queue.IndexOf(client) + 1; // 0 = not queued
    }

    public Room? FindRoom(string id)
    {
        lock (_lock) return _rooms.TryGetValue(id, out var room) ? room : null;
    }

    public Room? FindRoomOf(IClientConnection client)
    {
        lock (_lock) return _rooms.Values.FirstOrDefault(r => r.Contains(client));
    }

    /// <summary>
    /// Removes a client wherever they are: queue, room, or spectating.
    /// Returns the room they left, if any, so the caller can finish cleanup.
    /// </summary>
    public Room? RemoveClient(IClientConnection client)
    {
        Room? room;
        lock (_lock)
        {
            _queue.Remove(client);
            room = _rooms.Values.FirstOrDefault(r => r.Contains(client));
        }
        room?.PlayerDisconnected(client);
        return room;
    }

    /// <summary>Snapshot of open rooms for the `rooms` listing.</summary>
    public List<Dictionary<string, object?>> ListRooms()
    {
        lock (_lock)
        {
            return _rooms.Values.Select(r => new Dictionary<string, object?>
            {
                ["id"] = r.Id,
                ["red"] = r.Red.Name,
                ["yellow"] = r.Yellow.Name,
                ["spectators"] = r.SpectatorCount,
                ["over"] = r.IsOver
            }).ToList();
        }
    }

    private void OnRoomClosed(Room room)
    {
        lock (_lock) _rooms.Remove(room.Id);
        room.Closed -= OnRoomClosed;
    }
}
