using System.Text.Json.Serialization;

namespace ECK1.CommandsAPI.Domain.Tasks;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TaskCreatedEvent), nameof(TaskCreatedEvent))]
[JsonDerivedType(typeof(TaskUpdatedEvent), nameof(TaskUpdatedEvent))]
[JsonDerivedType(typeof(TaskDeletedEvent), nameof(TaskDeletedEvent))]
public interface ITaskItemEvent : IDomainEvent
{
    Guid TaskId { get; }
}

public record TaskItemEvent(Guid TaskId) : ITaskItemEvent
{
    [JsonIgnore]
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid EventId { get; set; } = Guid.NewGuid();
    public int Version { get; set; }
}

public record TaskCreatedEvent(
    Guid TaskId,
    string Title,
    string Description,
    TaskStatus Status,
    TaskPriority Priority,
    string Assignee,
    DateTime? DueDateUtc,
    List<string> Tags,
    DateTime CreatedAt) : TaskItemEvent(TaskId);

public record TaskUpdatedEvent(
    Guid TaskId,
    string Title,
    string Description,
    TaskStatus Status,
    TaskPriority Priority,
    string Assignee,
    DateTime? DueDateUtc,
    List<string> Tags) : TaskItemEvent(TaskId);

public record TaskDeletedEvent(Guid TaskId) : TaskItemEvent(TaskId);
