using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using CrmPlatform.ServiceTemplate.Infrastructure.Messaging;

namespace CrmPlatform.TestStubs;

/// <summary>
/// In-memory Service Bus for local testing without Azure infrastructure.
/// Messages published to this bus are immediately delivered to registered consumers.
/// </summary>
public sealed class InMemoryServiceBus
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ServiceBusMessage>> _topics = new();

    /// <summary>Publish a message to a topic. Delivers synchronously to all subscribers.</summary>
    public Task PublishAsync(string topic, object messageBody, Guid tenantId)
    {
        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(messageBody))
        {
            MessageId = Guid.NewGuid().ToString(),
            ApplicationProperties = { ["tenantId"] = tenantId.ToString() }
        };

        var queue = _topics.GetOrAdd(topic, _ => new ConcurrentQueue<ServiceBusMessage>());
        queue.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Process pending messages for a topic. Returns count processed.</summary>
    public async Task<int> ProcessTopicAsync(string topic, Func<ServiceBusMessage, Task<bool>> handler)
    {
        if (!_topics.TryGetValue(topic, out var queue)) return 0;
        var count = 0;
        while (queue.TryDequeue(out var message))
        {
            if (await handler(message))
                count++;
        }
        return count;
    }

    /// <summary>Peek at pending message count for a topic.</summary>
    public int PendingCount(string topic) =>
        _topics.TryGetValue(topic, out var queue) ? queue.Count : 0;

    /// <summary>Clear all messages.</summary>
    public void Reset() => _topics.Clear();
}

/// <summary>
/// In-memory idempotency store for testing.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, DateTime> _processed = new();

    public Task<bool> HasBeenProcessedAsync(string messageId, CancellationToken ct = default) =>
        Task.FromResult(_processed.ContainsKey(messageId));

    public Task MarkProcessedAsync(string messageId, CancellationToken ct = default)
    {
        _processed[messageId] = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public void Reset() => _processed.Clear();
}
