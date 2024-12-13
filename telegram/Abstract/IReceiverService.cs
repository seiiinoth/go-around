namespace go_around.Abstract;

/// <summary>
/// A marker interface for Update Receiver service
/// </summary>
public interface IReceiverService
{
  Task ReceiveAsync(CancellationToken stoppingToken);
}