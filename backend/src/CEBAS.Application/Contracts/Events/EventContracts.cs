using System.Text.Json.Serialization;

namespace CEBAS.Application.Contracts.Events;

/// <summary>
/// Universal event envelope distributed through Redis Pub/Sub and SignalR.
/// </summary>
public sealed record OutboxEventEnvelope(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("aggregateType")] string AggregateType,
    [property: JsonPropertyName("aggregateId")] Guid AggregateId,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("actorId")] Guid? ActorId,
    [property: JsonPropertyName("recipientId")] Guid? RecipientId,
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("causationId")] string? CausationId,
    [property: JsonPropertyName("payload")] object Payload
);

public sealed record OutboxEventEnvelope<T>(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("aggregateType")] string AggregateType,
    [property: JsonPropertyName("aggregateId")] Guid AggregateId,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("actorId")] Guid? ActorId,
    [property: JsonPropertyName("recipientId")] Guid? RecipientId,
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("causationId")] string? CausationId,
    [property: JsonPropertyName("payload")] T Payload
);

// Payload definitions:

public sealed record PostCreatedPayload(
    Guid PostId,
    Guid AuthorId,
    string? Content,
    int MediaCount,
    DateTimeOffset CreatedAt
);

public sealed record PostLikedPayload(
    Guid PostId,
    Guid ActorUserId,
    int LikeCount,
    Guid AuthorId,
    DateTimeOffset OccurredAt
);

public sealed record PostUnlikedPayload(
    Guid PostId,
    Guid ActorUserId,
    int LikeCount,
    DateTimeOffset OccurredAt
);

public sealed record ReplyCreatedPayload(
    Guid ReplyId,
    Guid PostId,
    Guid AuthorUserId,
    Guid? ParentReplyId,
    int ReplyCount,
    DateTimeOffset OccurredAt
);

public sealed record FollowCreatedPayload(
    Guid FollowerId,
    Guid FollowingId,
    DateTimeOffset OccurredAt
);

public sealed record NotificationCreatedPayload(
    Guid NotificationId,
    Guid RecipientId,
    Guid ActorId,
    string NotificationType,
    Guid? TargetId,
    string? TargetType,
    DateTimeOffset CreatedAt
);
