namespace ECK1.FailedViewRebuilder.Data.Models;

public class JobHistory
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public bool? IsSuccess { get; set; }

    public string ErrorMessage { get; set; }
}

