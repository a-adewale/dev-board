using DevBoard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevBoard.Api.Data;

public sealed class DevBoardDbContext(DbContextOptions<DevBoardDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueComment> IssueComments => Set<IssueComment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.HasIndex(project => project.WorkspaceId);
            entity.Property(project => project.Name).HasMaxLength(100).IsRequired();
            entity.Property(project => project.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(project => new { project.WorkspaceId, project.Name }).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(project => project.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Issue>(entity =>
        {
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.Title).HasMaxLength(160).IsRequired();
            entity.Property(issue => issue.Description).HasMaxLength(2000).IsRequired();
            entity.Property(issue => issue.Status).HasConversion<string>().IsRequired();
            entity.Property(issue => issue.Priority).HasConversion<string>().IsRequired();
            entity.HasIndex(issue => new { issue.ProjectId, issue.Status });
            entity.HasOne<Project>().WithMany().HasForeignKey(issue => issue.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<IssueComment>(entity =>
        {
            entity.HasKey(comment => comment.Id);
            entity.Property(comment => comment.Body).HasMaxLength(4000).IsRequired();
            entity.Property(comment => comment.Author).HasMaxLength(100).IsRequired();
            entity.HasIndex(comment => new { comment.IssueId, comment.CreatedAtUtc });
            entity.HasOne<Issue>().WithMany().HasForeignKey(comment => comment.IssueId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Subject).IsUnique();
            entity.Property(user => user.Subject).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(160).IsRequired();
        });
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(workspace => workspace.Id);
            entity.Property(workspace => workspace.Name).HasMaxLength(120).IsRequired();
        });
        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasKey(member => new { member.WorkspaceId, member.UserId });
            entity.Property(member => member.Role).HasConversion<string>().IsRequired();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(member => member.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
