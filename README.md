# GameHub — Multiplayer Board-Game Server

A multiplayer Connect Four server in C# (.NET 8, zero dependencies). Players
connect over TCP, join a matchmaking queue, and play live games in rooms while
spectators watch and chat. No accounts, no database — just a server, a
protocol, and a console client.

## Quick start

```bash
# Terminal 1: start the server (default port 5000)
dotnet run --project src/GameHub -- serve

# Terminal 2 and 3: two players join
dotnet run --project src/GameHub -- play --name alice
dotnet run --project src/GameHub -- play --name bob
```

In each client, type `queue` to find an opponent. The first player to join
waits; the second is paired with them and the game starts. Play by typing a
column number (`1`–`7`).

```
| . . . . . . . |
| . . . . . . . |
| . . . R . . . |
| . . . R Y . . |
| . . Y R Y . . |
| . R Y R Y . . |
  1 2 3 4 5 6 7
alice's turn — type a column number 1-7.
```

## Client commands

| Command           | What it does                                  |
| ----------------- | --------------------------------------------- |
| `queue`           | Join matchmaking; paired FIFO with the longest-waiting player |
| `1`–`7`           | Drop a disc in that column (your turn only)   |
| `say <text>`      | Chat with everyone in your room               |
| `rooms`           | List games you can watch                      |
| `watch <room id>` | Spectate a game                               |
| `rematch`         | Ask for another game once one ends (both players must agree; colours swap) |
| `leave`           | Leave the room                                |
| `quit`            | Disconnect                                    |

## Features

- **Matchmaking lobby** — first-come-first-served queue; two waiting players
  are paired into a room automatically.
- **Rooms with spectators** — anyone can watch a live game and chat; spectators
  cannot move.
- **Rematches** — both players must ask; colours swap so the second player
  opens the next game.
- **Forfeits** — a player disconnecting mid-game hands the win to the opponent
  and closes the room.
- **Chat** — room-wide, for players and spectators alike.
- **Safe names** — player names are sanitized to letters, digits, `-` and `_`
  (max 20 characters).

## The protocol

Newline-delimited JSON, one message per line — simple enough to speak by hand
with `nc` or `telnet`. Every message is a flat object with a `type` field.

Client → server:

| Type       | Fields              | Meaning                        |
| ---------- | ------------------- | ------------------------------ |
| `hello`    | `name`              | Set your display name          |
| `queue`    | —                   | Join matchmaking               |
| `leave_queue` | —                | Leave matchmaking              |
| `move`     | `column` (0–6)      | Drop a disc                    |
| `chat`     | `text`              | Message the room               |
| `rooms`    | —                   | List open rooms                |
| `spectate` | `room`              | Watch a room                   |
| `rematch`  | —                   | Vote for a rematch             |
| `leave`    | —                   | Leave the room                 |

Server → client: `welcome`, `queued` (`position`), `game_start` (`room`,
`red`, `yellow`, `you_are`), `board` (`board` lines, `current`, `result`,
`winner`, `you_are`), `chat_msg`, `room_list`, `info`, `error`.

Example session:

```
> {"type":"hello","name":"alice"}
< {"type":"info","message":"Hi alice! Type 'queue' to find a game..."}
> {"type":"queue"}
< {"type":"queued","position":1}
< {"type":"game_start","room":"a1b2c3d4","red":"alice","yellow":"bob","you_are":"R"}
< {"type":"board","board":["| . . . . . . . |", ...],"current":"alice","result":"Ongoing","winner":null,"you_are":"R"}
```

## Design decisions

- **Pure rules engine.** `ConnectFour` has no I/O and no networking, so the
  win detection and move validation are trivially unit-testable. It checks
  only the four directions through the last move, so a win check is O(1).
- **Row 0 is the bottom row.** A dropped disc lands on the lowest empty cell,
  which keeps the board model and the rendering trivial.
- **Sessions, not sockets, own game state.** `GameSession` tracks the players,
  the move history, and rematch votes; `Room` owns the players, spectators,
  and broadcasting. The server layer only routes messages.
- **Rooms talk to an interface, not a socket.** `Lobby` and `Room` depend on
  `IClientConnection`, so all matchmaking and game flow is unit-testable with
  an in-memory fake — no socket needed.
- **One lock per room.** All state changes in a room serialize through a
  single lock, so concurrent moves and chats from different connections cannot
  interleave. The lobby has its own lock for the queue and room registry.
- **Best-effort sends.** A dead socket never takes the server down: sends
  swallow I/O errors and the read loop cleans the client up.

## Project layout

```
src/GameHub/
  Program.cs                 # serve / play entry points
  Game/
    ConnectFour.cs           # rules engine: drops, win/draw detection, rendering
    GameSession.cs           # match state: players, history, rematch votes
  Protocol/
    Wire.cs                  # newline-delimited JSON envelope + message types
  Server/
    GameServer.cs            # TCP listener, one task per connection
    ClientSession.cs         # per-connection read loop, message dispatch
    Lobby.cs                 # matchmaking queue + room registry
    Room.cs                  # one match: players, spectators, broadcasting
    IClientConnection.cs     # connection abstraction (socket or test fake)
  Client/
    GameClient.cs            # interactive console client
tests/GameHub.Tests/         # xUnit tests: engine, sessions, protocol, lobby, rooms
```

## Build & test

```bash
dotnet build
dotnet test   # 41 test cases: engine, sessions, protocol, lobby, rooms
```

## Tech stack

- C# / .NET 8, no third-party dependencies
- `System.Net.Sockets` (`TcpListener` / `TcpClient`) for networking
- `System.Text.Json` for the wire protocol
- xUnit for tests

## License

MIT
