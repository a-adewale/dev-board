namespace DevBoard.Api.Models;

public sealed class User
{
    private User() { }

    public User(string subject, string email, string displayName)
    {
        Id = Guid.NewGuid();
        Subject = subject;
        Email = email;
        DisplayName = displayName;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
