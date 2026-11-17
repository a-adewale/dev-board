namespace DevBoard.Api.Models;

public sealed class Workspace
{
    private Workspace() { }

    public Workspace(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
