using DevBoard.Api.Data;
using DevBoard.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Npgsql.EntityFrameworkCore.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);
const string FrontendDevelopmentPolicy = "FrontendDevelopment";

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
var authDomain = builder.Configuration["Auth0:Domain"];
var authAudience = builder.Configuration["Auth0:Audience"];
if (!string.IsNullOrWhiteSpace(authDomain) && !string.IsNullOrWhiteSpace(authAudience))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.Authority = $"https://{authDomain}/";
        options.Audience = authAudience;
    });
    builder.Services.AddAuthorization();
}
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});
var databaseConnection = builder.Configuration.GetConnectionString("DevBoard") ?? "Data Source=devboard-v4.db";
builder.Services.AddDbContext<DevBoardDbContext>(options =>
{
    if (databaseConnection.Contains("Host=", StringComparison.OrdinalIgnoreCase)) options.UseNpgsql(databaseConnection);
    else options.UseSqlite(databaseConnection);
});
builder.Services.AddHealthChecks().AddDbContextCheck<DevBoardDbContext>("database");
builder.Services.AddCors(options => options.AddPolicy(FrontendDevelopmentPolicy,
    policy => policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.UseCors(FrontendDevelopmentPolicy);
if (!string.IsNullOrWhiteSpace(authDomain) && !string.IsNullOrWhiteSpace(authAudience))
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.Use(async (context, next) =>
    {
        var isMutation = HttpMethods.IsPost(context.Request.Method) ||
                         HttpMethods.IsPut(context.Request.Method) ||
                         HttpMethods.IsDelete(context.Request.Method) ||
                         HttpMethods.IsPatch(context.Request.Method);
        var isPublicEndpoint = context.Request.Path.StartsWithSegments("/api/health") ||
                               context.Request.Path.StartsWithSegments("/api/ready");

        if (isMutation && !isPublicEndpoint && !(context.User.Identity?.IsAuthenticated ?? false))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Authentication is required." });
            return;
        }

        await next();
    });
}

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<DevBoardDbContext>();
    database.Database.Migrate();
}

app.MapGet("/api/health", () =>
        Results.Ok(new HealthResponse("healthy", "DevBoard.Api", DateTimeOffset.UtcNow)))
    .WithName("GetHealth").WithTags("System");
app.MapHealthChecks("/api/ready").RequireRateLimiting("api");

app.MapGet("/api/me", async (HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    if (!(httpContext.User.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
    var subject = httpContext.User.FindFirst("sub")?.Value;
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    var email = httpContext.User.FindFirst("email")?.Value ?? string.Empty;
    var name = httpContext.User.FindFirst("name")?.Value ?? email;
    var user = await database.Users.FirstOrDefaultAsync(item => item.Subject == subject, cancellationToken);
    if (user is null)
    {
        user = new User(subject, email, name);
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
    }
    return Results.Ok(new UserResponse(user.Id, user.Subject, user.Email, user.DisplayName, user.CreatedAtUtc));
});

app.MapGet("/api/workspaces", async (HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var subject = httpContext.User.FindFirst("sub")?.Value;
    if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
    var workspaces = await database.WorkspaceMembers.AsNoTracking().Where(member => database.Users.Any(user => user.Subject == subject && user.Id == member.UserId)).Join(database.Workspaces, member => member.WorkspaceId, workspace => workspace.Id, (member, workspace) => new WorkspaceResponse(workspace.Id, workspace.Name, member.Role, workspace.CreatedAtUtc)).ToListAsync(cancellationToken);
    return Results.Ok(workspaces);
});

app.MapGet("/api/workspaces/{workspaceId:guid}/members", async (Guid workspaceId, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    if (!await HasWorkspaceRole(httpContext, workspaceId, WorkspaceRole.Viewer, database, cancellationToken)) return Results.Forbid();
    var members = await database.WorkspaceMembers.Where(member => member.WorkspaceId == workspaceId).Join(database.Users, member => member.UserId, user => user.Id, (member, user) => new MemberResponse(user.Id, user.Email, user.DisplayName, member.Role, member.JoinedAtUtc)).ToListAsync(cancellationToken);
    return Results.Ok(members);
});

app.MapPost("/api/workspaces/{workspaceId:guid}/members", async (Guid workspaceId, AddMemberRequest request, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    if (!await HasWorkspaceRole(httpContext, workspaceId, WorkspaceRole.Admin, database, cancellationToken)) return Results.Forbid();
    var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
    if (email.Length is < 3 or > 320) return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["A valid email is required."] });
    var user = await database.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
    if (user is null) { user = new User($"pending:{Guid.NewGuid()}", email, email); database.Users.Add(user); await database.SaveChangesAsync(cancellationToken); }
    if (await database.WorkspaceMembers.AnyAsync(member => member.WorkspaceId == workspaceId && member.UserId == user.Id, cancellationToken)) return Results.Conflict(new { message = "User is already a workspace member." });
    database.WorkspaceMembers.Add(new WorkspaceMember(workspaceId, user.Id, request.Role)); await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/workspaces/{workspaceId}/members/{user.Id}", new MemberResponse(user.Id, user.Email, user.DisplayName, request.Role, DateTimeOffset.UtcNow));
});

app.MapPut("/api/workspaces/{workspaceId:guid}/members/{userId:guid}", async (Guid workspaceId, Guid userId, ChangeRoleRequest request, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    if (!await HasWorkspaceRole(httpContext, workspaceId, WorkspaceRole.Admin, database, cancellationToken)) return Results.Forbid();
    var member = await database.WorkspaceMembers.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId, cancellationToken);
    if (member is null) return Results.NotFound();
    member.ChangeRole(request.Role); await database.SaveChangesAsync(cancellationToken); return Results.NoContent();
});

app.MapDelete("/api/workspaces/{workspaceId:guid}/members/{userId:guid}", async (Guid workspaceId, Guid userId, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    if (!await HasWorkspaceRole(httpContext, workspaceId, WorkspaceRole.Admin, database, cancellationToken)) return Results.Forbid();
    var member = await database.WorkspaceMembers.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId, cancellationToken);
    if (member is null) return Results.NotFound();
    database.WorkspaceMembers.Remove(member); await database.SaveChangesAsync(cancellationToken); return Results.NoContent();
});

app.MapGet("/api/projects", async (HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    var projects = (await database.Projects.AsNoTracking().Where(project => project.WorkspaceId == workspaceId)
        .Select(project => new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAtUtc))
        .ToListAsync(cancellationToken))
        .OrderByDescending(project => project.CreatedAtUtc)
        .ToList();
    return Results.Ok(projects);
});

app.MapGet("/api/projects/{id:guid}", async (Guid id, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    var project = await database.Projects.AsNoTracking().FirstOrDefaultAsync(project => project.Id == id && project.WorkspaceId == workspaceId, cancellationToken);
    return project is null
        ? Results.NotFound(new { message = "Project was not found." })
        : Results.Ok(new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAtUtc));
});

app.MapPost("/api/projects", async (CreateProjectRequest request, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    var name = request.Name?.Trim() ?? string.Empty;
    var description = request.Description?.Trim() ?? string.Empty;
    var errors = new Dictionary<string, string[]>();
    if (name.Length is < 3 or > 100) errors["name"] = ["Name must be between 3 and 100 characters."];
    if (description.Length > 500) errors["description"] = ["Description cannot exceed 500 characters."];
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    if (await database.Projects.AnyAsync(project => project.WorkspaceId == workspaceId && project.Name == name, cancellationToken))
        return Results.Conflict(new { message = "A project with this name already exists." });

    var project = new Project(workspaceId, name, description);
    database.Projects.Add(project);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/projects/{project.Id}",
        new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAtUtc));
});

app.MapPut("/api/projects/{id:guid}", async (Guid id, CreateProjectRequest request, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    var project = await database.Projects.FirstOrDefaultAsync(project => project.Id == id && project.WorkspaceId == workspaceId, cancellationToken);
    if (project is null) return Results.NotFound(new { message = "Project was not found." });

    var name = request.Name?.Trim() ?? string.Empty;
    var description = request.Description?.Trim() ?? string.Empty;
    var errors = new Dictionary<string, string[]>();
    if (name.Length is < 3 or > 100) errors["name"] = ["Name must be between 3 and 100 characters."];
    if (description.Length > 500) errors["description"] = ["Description cannot exceed 500 characters."];
    if (errors.Count > 0) return Results.ValidationProblem(errors);

    var duplicate = await database.Projects.AnyAsync(other => other.Id != id && other.WorkspaceId == workspaceId && other.Name == name, cancellationToken);
    if (duplicate) return Results.Conflict(new { message = "A project with this name already exists." });

    project.UpdateDetails(name, description);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAtUtc));
});

app.MapGet("/api/projects/{projectId:guid}/issues", async (Guid projectId, string? search, IssueStatus? status, IssuePriority? priority, int page, int pageSize, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    if (!await database.Projects.AnyAsync(project => project.Id == projectId && project.WorkspaceId == workspaceId, cancellationToken)) return Results.NotFound(new { message = "Project was not found." });
    page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 100);
    var query = database.Issues.AsNoTracking().Where(issue => issue.ProjectId == projectId);
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(issue => issue.Title.Contains(search) || issue.Description.Contains(search));
    if (status.HasValue) query = query.Where(issue => issue.Status == status);
    if (priority.HasValue) query = query.Where(issue => issue.Priority == priority);
    var total = await query.CountAsync(cancellationToken);
    var issues = await query.OrderByDescending(issue => issue.UpdatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
        .Select(issue => new IssueResponse(issue.Id, issue.ProjectId, issue.Title, issue.Description, issue.Status, issue.Priority, issue.CreatedAtUtc, issue.UpdatedAtUtc)).ToListAsync(cancellationToken);
    return Results.Ok(new PagedResult<IssueResponse>(issues, page, pageSize, total));
});

app.MapPost("/api/projects/{projectId:guid}/issues", async (Guid projectId, CreateIssueRequest request, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    if (!await database.Projects.AnyAsync(project => project.Id == projectId && project.WorkspaceId == workspaceId, cancellationToken))
        return Results.NotFound(new { message = "Project was not found." });
    var title = request.Title?.Trim() ?? string.Empty;
    var description = request.Description?.Trim() ?? string.Empty;
    var errors = ValidateIssue(title, description, request.Status, request.Priority);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var issue = new Issue(projectId, title, description, request.Status, request.Priority);
    database.Issues.Add(issue);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/issues/{issue.Id}", ToIssueResponse(issue));
});

app.MapPut("/api/issues/{id:guid}", async (Guid id, UpdateIssueRequest request, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var issue = await database.Issues.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    if (issue is null) return Results.NotFound(new { message = "Issue was not found." });
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    if (!await database.Projects.AnyAsync(project => project.Id == issue.ProjectId && project.WorkspaceId == workspaceId, cancellationToken)) return Results.NotFound(new { message = "Issue was not found." });
    var title = request.Title?.Trim() ?? string.Empty;
    var description = request.Description?.Trim() ?? string.Empty;
    var errors = ValidateIssue(title, description, request.Status, request.Priority);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    issue.Update(title, description, request.Status, request.Priority);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToIssueResponse(issue));
});

app.MapGet("/api/issues/{issueId:guid}/comments", async (Guid issueId, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    if (!await database.Issues.AnyAsync(issue => issue.Id == issueId && database.Projects.Any(project => project.Id == issue.ProjectId && project.WorkspaceId == workspaceId), cancellationToken)) return Results.NotFound();
    return Results.Ok(await database.IssueComments.AsNoTracking().Where(comment => comment.IssueId == issueId).OrderBy(comment => comment.CreatedAtUtc).Select(comment => new CommentResponse(comment.Id, comment.IssueId, comment.Body, comment.Author, comment.CreatedAtUtc)).ToListAsync(cancellationToken));
});

app.MapPost("/api/issues/{issueId:guid}/comments", async (Guid issueId, CreateCommentRequest request, HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var workspaceId = await GetWorkspaceId(httpContext, database, cancellationToken);
    if (!await database.Issues.AnyAsync(issue => issue.Id == issueId && database.Projects.Any(project => project.Id == issue.ProjectId && project.WorkspaceId == workspaceId), cancellationToken)) return Results.NotFound(new { message = "Issue was not found." });
    var body = request.Body?.Trim() ?? string.Empty;
    if (body.Length is < 1 or > 4000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Comment must be between 1 and 4000 characters."] });
    var comment = new IssueComment(issueId, body, string.IsNullOrWhiteSpace(request.Author) ? "Demo user" : request.Author.Trim());
    database.IssueComments.Add(comment); await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/issues/{issueId}/comments/{comment.Id}", new CommentResponse(comment.Id, comment.IssueId, comment.Body, comment.Author, comment.CreatedAtUtc));
});

app.MapDelete("/api/issues/{id:guid}", async (Guid id, DevBoardDbContext database, CancellationToken cancellationToken) =>
{
    var issue = await database.Issues.FindAsync([id], cancellationToken);
    if (issue is null) return Results.NotFound();
    database.Issues.Remove(issue);
    await database.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

static Dictionary<string, string[]> ValidateIssue(string title, string description, IssueStatus status, IssuePriority priority)
{
    var errors = new Dictionary<string, string[]>();
    if (title.Length is < 3 or > 160) errors["title"] = ["Title must be between 3 and 160 characters."];
    if (description.Length > 2000) errors["description"] = ["Description cannot exceed 2000 characters."];
    if (!Enum.IsDefined(status)) errors["status"] = ["Status is invalid."];
    if (!Enum.IsDefined(priority)) errors["priority"] = ["Priority is invalid."];
    return errors;
}

static async Task<Guid> GetWorkspaceId(HttpContext httpContext, DevBoardDbContext database, CancellationToken cancellationToken)
{
    var subject = httpContext.User.FindFirst("sub")?.Value ?? "local-development";
    var user = await database.Users.FirstOrDefaultAsync(item => item.Subject == subject, cancellationToken);
    if (user is null)
    {
        user = new User(subject, httpContext.User.FindFirst("email")?.Value ?? "local@devboard.test", httpContext.User.FindFirst("name")?.Value ?? "Local developer");
        database.Users.Add(user);
        await database.SaveChangesAsync(cancellationToken);
    }

    var membership = await database.WorkspaceMembers.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
    if (membership is not null) return membership.WorkspaceId;

    var workspace = new Workspace($"{user.DisplayName}'s workspace");
    database.Workspaces.Add(workspace);
    database.WorkspaceMembers.Add(new WorkspaceMember(workspace.Id, user.Id, WorkspaceRole.Owner));
    await database.SaveChangesAsync(cancellationToken);
    return workspace.Id;
}

static async Task<bool> HasWorkspaceRole(HttpContext httpContext, Guid workspaceId, WorkspaceRole minimumRole, DevBoardDbContext database, CancellationToken cancellationToken)
{
    var subject = httpContext.User.FindFirst("sub")?.Value ?? "local-development";
    var role = await database.WorkspaceMembers.Where(member => member.WorkspaceId == workspaceId).Join(database.Users.Where(user => user.Subject == subject), member => member.UserId, user => user.Id, (member, _) => member.Role).FirstOrDefaultAsync(cancellationToken);
    return role >= minimumRole;
}

static IssueResponse ToIssueResponse(Issue issue) => new(issue.Id, issue.ProjectId, issue.Title, issue.Description, issue.Status, issue.Priority, issue.CreatedAtUtc, issue.UpdatedAtUtc);

app.Run();

public sealed record HealthResponse(string Status, string Service, DateTimeOffset CheckedAtUtc);
public sealed record CreateProjectRequest(string? Name, string? Description);
public sealed record ProjectResponse(Guid Id, string Name, string Description, DateTimeOffset CreatedAtUtc);
public sealed record CreateIssueRequest(string? Title, string? Description, IssueStatus Status = IssueStatus.Backlog, IssuePriority Priority = IssuePriority.Medium);
public sealed record UpdateIssueRequest(string? Title, string? Description, IssueStatus Status, IssuePriority Priority = IssuePriority.Medium);
public sealed record IssueResponse(Guid Id, Guid ProjectId, string Title, string Description, IssueStatus Status, IssuePriority Priority, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record CreateCommentRequest(string? Body, string? Author);
public sealed record CommentResponse(Guid Id, Guid IssueId, string Body, string Author, DateTimeOffset CreatedAtUtc);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
public sealed record UserResponse(Guid Id, string Subject, string Email, string DisplayName, DateTimeOffset CreatedAtUtc);
public sealed record WorkspaceResponse(Guid Id, string Name, WorkspaceRole Role, DateTimeOffset CreatedAtUtc);
public sealed record AddMemberRequest(string? Email, WorkspaceRole Role = WorkspaceRole.Member);
public sealed record ChangeRoleRequest(WorkspaceRole Role);
public sealed record MemberResponse(Guid UserId, string Email, string DisplayName, WorkspaceRole Role, DateTimeOffset JoinedAtUtc);
public partial class Program;
