namespace TacomaDiag.Services;

public interface IObdTransport : IDisposable
{
    bool IsConnected { get; }
    string ConnectionName { get; }
    Task<string> SendCommandAsync(string command, int timeoutMs = 4000, CancellationToken cancellationToken = default);
    void Disconnect();
}
