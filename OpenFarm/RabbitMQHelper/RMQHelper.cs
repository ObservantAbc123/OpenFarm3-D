using System.Text;
using System.Text.Json;
using RabbitMQHelper.MessageTypes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace RabbitMQHelper;

/// <summary>
/// Maintains a connection to RabbitMQ for sending messages and attaching listeners to queues.
/// </summary>
public class RmqHelper : IDisposable, IAsyncDisposable, IRmqHelper
{
    private IChannel? _channel;
    private IConnection? _connection;

    /// <summary>
    /// Attempts a connection to the RMQ server. If previously connected, will close that connection and try again.
    /// Limited to 3 retries with 1 second delay between them.
    /// </summary>
    /// <returns></returns>
    public async Task<bool> Connect()
    {
        string? rmqUser = Environment.GetEnvironmentVariable("RABBITMQ_USER");
        string? rmqPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD");
        string? rmqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
        string? nonetwork = Environment.GetEnvironmentVariable("nonetwork");

        // Close previous connection if it existed.
        if (_connection is not null)
            await _connection.CloseAsync();

        int retryCount = 0;
        while (retryCount < 3)
        {
            try
            {
                if (nonetwork is not null)
                    rmqHost = "localhost";

                // Create the connection and attempt to connect
                ConnectionFactory factory = new()
                {
                    Uri = new Uri($"amqp://{rmqUser}:{rmqPassword}@{rmqHost}:5672"),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
                };
                using Task<IConnection> connectionTask = factory.CreateConnectionAsync();
                connectionTask.Wait();
                _connection = connectionTask.Result;
                // Attach callbacks for connection events.
                _connection.RecoverySucceededAsync += ConnectionOnRecoverySucceededAsync;
                _connection.ConnectionRecoveryErrorAsync += ConnectionOnConnectionRecoveryErrorAsync;
                _connection.ConnectionBlockedAsync += ConnectionOnConnectionBlockedAsync;
                _connection.ConnectionShutdownAsync += ConnectionOnConnectionShutdownAsync;
                // Create channel and wait for it to be ready
                using Task<IChannel> channelAsync = _connection.CreateChannelAsync();
                channelAsync.Wait();
                _channel = channelAsync.Result;
                // Attach callbacks for channel events.
                _channel.ChannelShutdownAsync += ChannelOnChannelShutdownAsync;
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

                return true;
            }
            catch (BrokerUnreachableException)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                retryCount++;
            }
        }

        return false;
    }

    public bool AddListener<T>(QueueNames queue, Func<T, bool> listener) where T : Message
    {
        if (!IsConnected())
            return false;

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.ReceivedAsync += (model, eventArgs) =>
        {
            try
            {
                T? msg = JsonSerializer.Deserialize<T>(eventArgs.Body.ToArray());
                bool result = listener(msg!);
                if (result)
                    return _channel!.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false).AsTask();
                return Task.FromException(new Exception("Failed to complete listener method in RMQHelper."));
            }
            catch (Exception ex)
            {
                return _channel!.BasicNackAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: true).AsTask();
            }
        };
        var task = _channel!.BasicConsumeAsync(queue.ToString(), false, consumer);
        task.Wait();
        return task.IsCompletedSuccessfully;
    }

    /// <summary>
    /// If the channel is shutdown, attempt to refresh it by retrying the connection.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    private Task ChannelOnChannelShutdownAsync(object sender, ShutdownEventArgs @event)
    {
        Console.WriteLine($"RMQ Channel shutdown! Initiated by: {@event.Initiator} Cause: {@event.Cause}!");
        return Connect();
    }

    /// <summary>
    /// Logs when an automatic connection recovery fails.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    private Task ConnectionOnConnectionRecoveryErrorAsync(object sender, ConnectionRecoveryErrorEventArgs @event)
    {
        Console.WriteLine($"RMQ Connection recovery failed: {@event.Exception.Message}!");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs a connection shutdown event.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    private Task ConnectionOnConnectionShutdownAsync(object sender, ShutdownEventArgs @event)
    {
        Console.WriteLine(
            $"RMQ Connection shutdown! Initiated by: {@event.Initiator} Exception message: {@event.Exception?.Message}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Logs when a connection is blocked by RMQ. This shouldn't happen if RMQ is functioning normally.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    private Task ConnectionOnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs @event)
    {
        Console.WriteLine($"RMQ Connection blocked: {@event.Reason}!");
        return Task.CompletedTask;
    }

    /// <summary>
    ///  Logs a successful connection recovery event. RMQ will automatically attempt recovery when the connection is lost.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    private Task ConnectionOnRecoverySucceededAsync(object sender, AsyncEventArgs @event)
    {
        Console.WriteLine($"RMQ Connection recovered!");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task QueueMessage(ExchangeNames exchange, Message message)
    {
        return PublishMessage(exchange, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
    }

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task QueueMessage(ExchangeNames exchange, RejectMessage message)
    {
        return PublishMessage(exchange, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
    }

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task QueueMessage(ExchangeNames exchange, AcceptMessage message)
    {
        return PublishMessage(exchange, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
    }

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task QueueMessage(ExchangeNames exchange, PrintStartedMessage message)
    {
        return PublishMessage(exchange, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
    }

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task QueueMessage(ExchangeNames exchange, PrintFinishedMessage message)
    {
        return PublishMessage(exchange, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
    }

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task QueueMessage(ExchangeNames exchange, PrintClearedMessage message)
    {
        return PublishMessage(exchange, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
    }

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task QueueMessage(ExchangeNames exchange, OperatorReplyMessage message)
    {
        return PublishMessage(exchange, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)));
    }

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="payload"></param>
    /// <returns></returns>
    private Task PublishMessage(ExchangeNames exchange, byte[] payload)
    {
        if (!IsConnected())
            return Task.FromException(new ConnectFailureException("No connection to RMQ", new Exception()));

        return _channel!.BasicPublishAsync(exchange: exchange.ToString(), routingKey: "", body: payload).AsTask();
    }

    /// <summary>
    /// Checks that both the connection and channel exist and are available for use.
    /// </summary>
    /// <returns>True if the RMQ connections are ready for use, false otherwise</returns>
    public bool IsConnected()
    {
        return (_connection != null && _channel != null) && (_connection.IsOpen && _channel.IsOpen);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null) await _channel.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }
}
