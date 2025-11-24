using RabbitMQHelper.MessageTypes;

namespace RabbitMQHelper;

public interface IRmqHelper
{
    /// <summary>
    /// Attempts a connection to RabbitMQ. Must be done before using the helper.
    /// </summary>
    Task<bool> Connect();

    /// <summary>
    /// Attaches a listener to the provided queue.
    /// </summary>
    /// <param name="queue">Queue to attach listener to.</param>
    /// <param name="listener">Listener to attach to queue. Will be passed the Message that is the message body.</param>
    /// <returns>True if listener added, false if there was a failure. Likely due to faulty RMQ connection</returns>
    bool AddListener<T>(QueueNames queue, Func<T, bool> listener) where T : Message;

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"> Exchange to publish message to. </param>
    /// <param name="message"> Message to serialize and publish to exchange. </param>
    /// <returns></returns>
    Task QueueMessage(ExchangeNames exchange, Message message);

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"> Exchange to publish message to. </param>
    /// <param name="message"> Message to serialize and publish to exchange. </param>
    /// <returns></returns>
    Task QueueMessage(ExchangeNames exchange, RejectMessage message);

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"> Exchange to publish message to. </param>
    /// <param name="message"> Message to serialize and publish to exchange. </param>
    /// <returns></returns>
    Task QueueMessage(ExchangeNames exchange, AcceptMessage message);

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"> Exchange to publish message to. </param>
    /// <param name="message"> Message to serialize and publish to exchange. </param>
    /// <returns></returns>
    Task QueueMessage(ExchangeNames exchange, PrintStartedMessage message);

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"> Exchange to publish message to. </param>
    /// <param name="message"> Message to serialize and publish to exchange. </param>
    /// <returns></returns>
    Task QueueMessage(ExchangeNames exchange, PrintFinishedMessage message);

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"> Exchange to publish message to. </param>
    /// <param name="message"> Message to serialize and publish to exchange. </param>
    /// <returns></returns>
    Task QueueMessage(ExchangeNames exchange, PrintClearedMessage message);

    /// <summary>
    /// Queues the message to go to the specified exchange.
    /// </summary>
    /// <param name="exchange"> Exchange to publish message to. </param>
    /// <param name="message"> Message to serialize and publish to exchange. </param>
    /// <returns></returns>
    Task QueueMessage(ExchangeNames exchange, OperatorReplyMessage message);

    void Dispose();
    ValueTask DisposeAsync();
    bool IsConnected();
}
