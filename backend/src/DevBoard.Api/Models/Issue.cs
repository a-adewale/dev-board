namespace DevBoard.Api.Models;

public sealed class Issue
{
    private Issue() { }

    public Issue(Guid projectId, string title, string description, IssueStatus status, IssuePriority priority = IssuePriority.Medium)
    {
        Id = Guid.NewGuid();
        ProjectId = projectId;
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public IssueStatus Status { get; private set; }
    public IssuePriority Priority { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(string title, string description, IssueStatus status, IssuePriority priority)
    {
        Title = title;
        Description = description;
        Status = status;
        Priority = priority;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
