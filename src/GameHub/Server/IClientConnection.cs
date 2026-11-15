namespace GameHub.Server;

/// <summary>
/// What the room and lobby talk to: a single connected client. The TCP
/// implementation lives in <see cref="ClientSession"/>; tests substitute a
/// fake, so all matchmaking and game flow is testable without a socket.
/// </summary>
public interface IClientConnection
{
    string Id { get; }
    string Name { get; set; }
    void Send(string json);
    void Close();
}
