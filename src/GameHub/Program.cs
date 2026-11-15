// GameHub — multiplayer board-game server (Connect Four).
using GameHub.Server;

if (args.Length == 0)
{
    PrintUsage();
    return 0;
}

switch (args[0])
{
    case "serve":
    {
        var port = ArgValue(args, "--port", 5000);
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        var server = new GameServer(port);
        await server.RunAsync(cts.Token);
        return 0;
    }
    case "play":
        Console.WriteLine("The play client lands in the next commit. For now, talk to the");
        Console.WriteLine("server with any TCP client — it speaks newline-delimited JSON.");
        return 0;
    default:
        Console.WriteLine($"Unknown command '{args[0]}'.");
        PrintUsage();
        return 1;
}

static void PrintUsage()
{
    Console.WriteLine("GameHub — multiplayer Connect Four server");
    Console.WriteLine("Usage:");
    Console.WriteLine("  gamehub serve [--port 5000]   Start the game server");
    Console.WriteLine("  gamehub play  [--host HOST] [--port 5000] [--name NAME]");
    Console.WriteLine("                                Join as a player");
}

static int ArgValue(string[] args, string flag, int fallback)
{
    var i = Array.IndexOf(args, flag);
    return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var value)
        ? value
        : fallback;
}
