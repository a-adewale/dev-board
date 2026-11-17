namespace DevBoard.Api.Models;

public sealed class Project
{
    private Project() { }

    public Project(Guid workspaceId, string name, string description)
    {
        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        Name = name;
        Description = description;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void UpdateDetails(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
