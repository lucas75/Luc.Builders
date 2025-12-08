using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Lwx.Builders.MicroService.Atributtes;

namespace ExampleOrg.Product.ServiceAbc.Endpoints;

/// <summary>
/// Example in-memory queue provider for testing and development purposes.
/// This provider uses System.Threading.Channels to simulate queue behavior.
/// </summary>
public class ExampleQueueProvider : ILwxQueueProvider
{
    private readonly Channel<ILwxQueueMessage> _channel = Channel.CreateUnbounded<ILwxQueueMessage>();
    private ILwxProviderErrorPolicy? _providerErrorPolicy;
    private CancellationTokenSource? _cts;
    private readonly List<Task> _readers = new();

    /// <inheritdoc />
    public string Name => nameof(ExampleQueueProvider);

    /// <inheritdoc />
    public void Configure(IConfiguration configuration, string sectionName)
    {
        // Read settings from configuration.GetSection($"Queues:{sectionName}") 
        var section = configuration.GetSection($"Queues:{sectionName}");
        // Example: var maxRetries = section.GetValue<int>("MaxRetries", 3);
        Console.WriteLine($"[{Name}] Configured with section: Queues:{sectionName}");
    }

    /// <inheritdoc />
    public void SetProviderErrorPolicy(ILwxProviderErrorPolicy policy)
    {
        _providerErrorPolicy = policy;
    }

    /// <inheritdoc />
    public Task StartAsync(Func<ILwxQueueMessage, CancellationToken, Task> handler, int concurrency, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        for (int i = 0; i < concurrency; i++)
        {
            var readerIndex = i;
            var readerTask = Task.Run(async () =>
            {
                Console.WriteLine($"[{Name}] Reader {readerIndex} started");
                try
                {
                    await foreach (var msg in _channel.Reader.ReadAllAsync(_cts.Token))
                    {
                        try
                        {
                            await handler(msg, _cts.Token);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            Console.WriteLine($"[{Name}] Handler error for message {msg.MessageId}: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{Name}] Reader {readerIndex} error: {ex.Message}");
                    if (_providerErrorPolicy != null)
                    {
                        await _providerErrorPolicy.HandleProviderErrorAsync(ex, ct);
                    }
                }
                Console.WriteLine($"[{Name}] Reader {readerIndex} stopped");
            }, _cts.Token);
            
            _readers.Add(readerTask);
        }
        
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct)
    {
        _channel.Writer.Complete();
        
        if (_cts != null)
        {
            await _cts.CancelAsync();
        }

        await Task.WhenAll(_readers);
        _readers.Clear();
        
        Console.WriteLine($"[{Name}] Stopped");
    }

    /// <summary>
    /// Enqueues a message for processing. Used for testing.
    /// </summary>
    public async ValueTask EnqueueAsync(ILwxQueueMessage message)
    {
        await _channel.Writer.WriteAsync(message);
    }
}

/// <summary>
/// Example queue message implementation for testing.
/// </summary>
internal class ExampleQueueMessage : ILwxQueueMessage
{
    public ExampleQueueMessage(string payload, Dictionary<string, string>? headers = null)
    {
        MessageId = Guid.NewGuid().ToString("N");
        Payload = System.Text.Encoding.UTF8.GetBytes(payload);
        Headers = headers ?? new Dictionary<string, string>();
        EnqueuedAt = DateTimeOffset.UtcNow;
    }

    public string MessageId { get; }
    public ReadOnlyMemory<byte> Payload { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public DateTimeOffset EnqueuedAt { get; }

    public ValueTask CompleteAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[ExampleQueueMessage] Message {MessageId} completed");
        return ValueTask.CompletedTask;
    }

    public ValueTask AbandonAsync(string? reason = null, CancellationToken ct = default)
    {
        Console.WriteLine($"[ExampleQueueMessage] Message {MessageId} abandoned: {reason}");
        return ValueTask.CompletedTask;
    }

    public ValueTask DeadLetterAsync(string? reason = null, CancellationToken ct = default)
    {
        Console.WriteLine($"[ExampleQueueMessage] Message {MessageId} dead-lettered: {reason}");
        return ValueTask.CompletedTask;
    }
}
