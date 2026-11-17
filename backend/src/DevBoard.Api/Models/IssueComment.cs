namespace DevBoard.Api.Models;

public sealed class IssueComment
{
    private IssueComment() { }

    public IssueComment(Guid issueId, string body, string author)
    {
        Id = Guid.NewGuid();
        IssueId = issueId;
        Body = body;
        Author = author;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
