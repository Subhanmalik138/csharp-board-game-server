# GameHub — Multiplayer Board-Game Server

A multiplayer Connect Four server in C# (.NET 8, zero dependencies). Players
connect over TCP, join a matchmaking queue, and play live games in rooms while
spectators watch and chat.

> 🚧 Work in progress — this commit lands the pure game engine. The TCP
> server, matchmaking lobby, and CLI client follow in the next commits.

## The game

Standard Connect Four: 7 columns × 6 rows, first to four in a row wins —
horizontal, vertical, or diagonal.

## Design decisions

- **Pure rules engine.** `ConnectFour` has no I/O and no networking, so the
  win detection and move validation are trivially unit-testable.
- **Row 0 is the bottom row.** A dropped disc lands on the lowest empty cell,
  which keeps the win check to four directions from the last move.
- **Sessions, not sockets, own game state.** `GameSession` tracks the players,
  the move history, and rematch votes, so the server layer only routes
  messages.

## Project layout

```
src/GameHub/
  Game/ConnectFour.cs    # rules engine: drops, win/draw detection, rendering
  Game/GameSession.cs    # match state: players, history, rematch votes
tests/GameHub.Tests/     # xUnit tests (engine, protocol, lobby)
```

## Build & test

```bash
dotnet build
dotnet test
```

## License

MIT
