namespace ECK1.CommandsAPI.Domain.Tasks;

public class TaskItem : AggregateRoot<ITaskItemEvent>
{
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);

    public Guid TaskId => Id;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public string Assignee { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public IReadOnlyCollection<string> Tags => _tags.ToList().AsReadOnly();
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private TaskItem() { }

    public static TaskItem Create(
        string title,
        string description,
        TaskStatus status,
        TaskPriority priority,
        string assignee,
        DateTime? dueDateUtc,
        List<string> tags)
    {
        Validate(title);
        var root = AggregateRoot.CreateNew<TaskItem>();
        root.ApplyChange(new TaskCreatedEvent(
            Guid.NewGuid(), title.Trim(), description?.Trim() ?? string.Empty, status, priority,
            NormalizeOptional(assignee), NormalizeUtc(dueDateUtc), NormalizeTags(tags), DateTime.UtcNow));
        return root;
    }

    public void Update(
        string title,
        string description,
        TaskStatus status,
        TaskPriority priority,
        string assignee,
        DateTime? dueDateUtc,
        List<string> tags)
    {
        EnsureActive();
        Validate(title);
        ApplyChange(new TaskUpdatedEvent(
            Id, title.Trim(), description?.Trim() ?? string.Empty, status, priority,
            NormalizeOptional(assignee), NormalizeUtc(dueDateUtc), NormalizeTags(tags)));
    }

    public void Delete()
    {
        EnsureActive();
        ApplyChange(new TaskDeletedEvent(Id));
    }

    private static void Validate(string title) => ArgumentException.ThrowIfNullOrWhiteSpace(title);

    private void EnsureActive()
    {
        if (IsDeleted)
            throw new InvalidOperationException("Deleted tasks cannot be changed.");
    }

    private static string NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? NormalizeUtc(DateTime? value) => value?.ToUniversalTime();

    private static List<string> NormalizeTags(IEnumerable<string> tags) =>
        (tags ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void Apply(TaskCreatedEvent @event)
    {
        Id = @event.TaskId;
        SetFields(@event.Title, @event.Description, @event.Status, @event.Priority,
            @event.Assignee, @event.DueDateUtc, @event.Tags);
        CreatedAt = @event.CreatedAt;
        IsDeleted = false;
    }

    private void Apply(TaskUpdatedEvent @event) =>
        SetFields(@event.Title, @event.Description, @event.Status, @event.Priority,
            @event.Assignee, @event.DueDateUtc, @event.Tags);

    private void Apply(TaskDeletedEvent _) => IsDeleted = true;

    private void SetFields(
        string title,
        string description,
        TaskStatus status,
        TaskPriority priority,
        string assignee,
        DateTime? dueDateUtc,
        IEnumerable<string> tags)
    {
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        Assignee = assignee;
        DueDateUtc = dueDateUtc;
        _tags.Clear();
        foreach (var tag in tags)
            _tags.Add(tag);
    }

    protected override IAggregateRootReplay DeepClone()
    {
        var copy = new TaskItem
        {
            Id = Id,
            Version = Version,
            Title = Title,
            Description = Description,
            Status = Status,
            Priority = Priority,
            Assignee = Assignee,
            DueDateUtc = DueDateUtc,
            CreatedAt = CreatedAt,
            IsDeleted = IsDeleted,
        };
        foreach (var tag in _tags)
            copy._tags.Add(tag);
        return copy;
    }
}
