using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Lwx.Builders.MicroService.Atributtes;

namespace ExampleOrg.Product.ServiceAbc.Dto;

/// <summary>
/// Order message DTO that implements ILwxQueueMessage for unified processing
/// across both HTTP and queue channels.
/// </summary>
public class OrderMessage : ILwxQueueMessage
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The order payload as a string (JSON content).
    /// </summary>
    [JsonPropertyName("payload")]
    public string PayloadString { get; set; } = string.Empty;

    /// <summary>
    /// The raw message payload as bytes.
    /// </summary>
    [JsonIgnore]
    public ReadOnlyMemory<byte> Payload => System.Text.Encoding.UTF8.GetBytes(PayloadString);

    /// <summary>
    /// Message headers/properties.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string> HeadersDict { get; set; } = new();

    /// <summary>
    /// Message headers as read-only dictionary.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, string> Headers => HeadersDict;

    /// <summary>
    /// Timestamp when the message was enqueued.
    /// </summary>
    [JsonPropertyName("enqueuedAt")]
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public ValueTask CompleteAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask AbandonAsync(string? reason = null, CancellationToken ct = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DeadLetterAsync(string? reason = null, CancellationToken ct = default) => ValueTask.CompletedTask;
}
