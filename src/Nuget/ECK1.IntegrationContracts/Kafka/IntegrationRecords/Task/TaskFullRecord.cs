using ECK1.IntegrationContracts.Abstractions;
using ProtoBuf;

namespace ECK1.IntegrationContracts.Kafka.IntegrationRecords.Task;

[ProtoContract]
public class TaskFullRecord : IIntegrationMessage
{
    [ProtoIgnore] public string Id => TaskId.ToString();

    [ProtoMember(1)] public Guid TaskId { get; set; }
    [ProtoMember(2)] public string Title { get; set; } = string.Empty;
    [ProtoMember(3)] public string Description { get; set; } = string.Empty;
    [ProtoMember(4)] public TaskItemStatus Status { get; set; }
    [ProtoMember(5)] public TaskItemPriority Priority { get; set; }
    [ProtoMember(6)] public string Assignee { get; set; }
    [ProtoMember(7)] public DateTime? DueDateUtc { get; set; }
    [ProtoMember(8)] public List<TaskTag> Tags { get; set; } = [];
    [ProtoMember(9)] public DateTime CreatedAt { get; set; }
    [ProtoMember(10)] public bool IsDeleted { get; set; }
    [ProtoMember(11)] public int Version { get; set; }
    [ProtoMember(12)] public DateTime OccuredAt { get; set; }
}

[ProtoContract]
public enum TaskItemStatus
{
    [ProtoEnum] ToDo = 0,
    [ProtoEnum] InProgress = 1,
    [ProtoEnum] Blocked = 2,
    [ProtoEnum] Done = 3,
    [ProtoEnum] Cancelled = 4,
}

[ProtoContract]
public enum TaskItemPriority
{
    [ProtoEnum] Low = 0,
    [ProtoEnum] Medium = 1,
    [ProtoEnum] High = 2,
    [ProtoEnum] Critical = 3,
}

[ProtoContract]
public class TaskTag
{
    [ProtoMember(1)] public string Value { get; set; } = string.Empty;
}
