namespace DevBoard.Api.Models;

public sealed class WorkspaceMember
{
    private WorkspaceMember() { }

    public WorkspaceMember(Guid workspaceId, Guid userId, WorkspaceRole role)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    public void ChangeRole(WorkspaceRole role) => Role = role;
}
