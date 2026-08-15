using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SundayLeague.Api.Auth;
using SundayLeague.Api.Data;
using SundayLeague.Api.Domain;

namespace SundayLeague.Api.Features;

public sealed class StandingsService(LeagueDbContext db)
{
    public async Task<IReadOnlyList<StandingRow>> GetAsync(Guid leagueId)
    {
        var league = await db.Leagues.SingleAsync(league => league.Id == leagueId);
        var teams = await db.Teams.Where(team => team.LeagueId == leagueId).OrderBy(team => team.Name).ToListAsync();
        var matches = await db.Matches.Where(match => match.LeagueId == leagueId && match.Status == MatchStatus.Confirmed).ToListAsync();
        var rows = teams.Select(team =>
        {
            var played = matches.Where(match => match.HomeTeamId == team.Id || match.AwayTeamId == team.Id).ToList();
            var goalsFor = played.Sum(match => match.HomeTeamId == team.Id ? match.HomeScore ?? 0 : match.AwayScore ?? 0);
            var goalsAgainst = played.Sum(match => match.HomeTeamId == team.Id ? match.AwayScore ?? 0 : match.HomeScore ?? 0);
            var won = played.Count(match => (match.HomeTeamId == team.Id ? match.HomeScore : match.AwayScore) > (match.HomeTeamId == team.Id ? match.AwayScore : match.HomeScore));
            var drawn = played.Count(match => match.HomeScore == match.AwayScore);
            var lost = played.Count - won - drawn;
            return new StandingRow(0, team.Id, team.Name, played.Count, won, drawn, lost, goalsFor, goalsAgainst, won * league.WinPoints + drawn * league.DrawPoints + lost * league.LossPoints);
        });
        var ordered = league.Tiebreaker == StandingTiebreaker.GoalsForThenGoalDifference
            ? rows.OrderByDescending(row => row.Points).ThenByDescending(row => row.GoalsFor).ThenByDescending(row => row.GoalDifference).ThenBy(row => row.TeamName)
            : rows.OrderByDescending(row => row.Points).ThenByDescending(row => row.GoalDifference).ThenByDescending(row => row.GoalsFor).ThenBy(row => row.TeamName);
        return ordered.Select((row, index) => row with { Position = index + 1 }).ToList();
    }
}

public sealed class InvitationService(LeagueDbContext db, UserManager<ApplicationUser> users, BoardAuthorizationService access, IConfiguration configuration)
{
    public async Task<ServiceResult<InvitationResponse>> CreateAsync(ClaimsPrincipal actor, CreateInvitationRequest request)
    {
        if (!await access.HasRoleAsync(actor, null, BoardRole.Owner)) return ServiceResult<InvitationResponse>.Denied();
        if (request.Role == BoardRole.Owner) return ServiceResult<InvitationResponse>.Invalid("Owner invitations are not permitted.");
        if ((request.Role is BoardRole.CompetitionAdmin or BoardRole.ResultsEditor) && request.LeagueId is null)
            return ServiceResult<InvitationResponse>.Invalid("Choose a league for this role.");
        if (request.LeagueId is not null && !await db.Leagues.AnyAsync(league => league.Id == request.LeagueId)) return ServiceResult<InvitationResponse>.Invalid("League not found.");
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var actorId = actor.FindFirstValue(ClaimTypes.NameIdentifier)!;
        db.BoardInvitations.Add(new BoardInvitation { Email = request.Email.Trim().ToLowerInvariant(), TokenHash = Hash(rawToken), Role = request.Role, LeagueId = request.LeagueId, CreatedByUserId = actorId, ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
        await db.SaveChangesAsync();
        var exposeCode = configuration.GetValue<bool>("Invitations:ExposeCodes");
        return ServiceResult<InvitationResponse>.Ok(new InvitationResponse(request.Email, request.Role, request.LeagueId, exposeCode ? rawToken : null));
    }

    public async Task<ServiceResult> AcceptAsync(RegisterRequest request)
    {
        var invitation = await db.BoardInvitations.SingleOrDefaultAsync(item => item.TokenHash == Hash(request.InvitationToken));
        if (invitation is null || invitation.AcceptedAt is not null || invitation.ExpiresAt <= DateTimeOffset.UtcNow) return ServiceResult.Invalid("This invitation is invalid or expired.");
        if (!string.Equals(invitation.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase)) return ServiceResult.Invalid("The email does not match the invitation.");
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), EmailConfirmed = true, DisplayName = request.DisplayName.Trim() };
        var create = await users.CreateAsync(user, request.Password);
        if (!create.Succeeded) return ServiceResult.Invalid(string.Join(" ", create.Errors.Select(error => error.Description)));
        db.BoardMemberships.Add(new BoardMembership { UserId = user.Id, LeagueId = invitation.LeagueId, Role = invitation.Role });
        invitation.AcceptedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class AuditService(LeagueDbContext db)
{
    public Task RecordAsync(ClaimsPrincipal actor, string action, string type, Guid id, string detail)
    {
        db.AuditEvents.Add(new AuditEvent { OccurredAt = DateTimeOffset.UtcNow, ActorUserId = actor.FindFirstValue(ClaimTypes.NameIdentifier), Action = action, EntityType = type, EntityId = id, Detail = detail });
        return Task.CompletedTask;
    }
}

/// <summary>Creates the first Owner only when credentials are supplied through user-secrets or environment variables.</summary>
public sealed class InitialOwnerService(LeagueDbContext db, UserManager<ApplicationUser> users, IConfiguration configuration)
{
    public async Task EnsureCreatedAsync()
    {
        if (await db.BoardMemberships.AnyAsync(membership => membership.Role == BoardRole.Owner)) return;
        var email = configuration["Bootstrap:OwnerEmail"];
        var password = configuration["Bootstrap:OwnerPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        var user = await users.FindByEmailAsync(email) ?? new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "League Owner" };
        if (string.IsNullOrEmpty(user.Id))
        {
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded) throw new InvalidOperationException("The bootstrap Owner could not be created.");
        }
        db.BoardMemberships.Add(new BoardMembership { UserId = user.Id, Role = BoardRole.Owner });
        await db.SaveChangesAsync();
    }
}

public sealed record ServiceResult(bool Success, string? Error = null)
{
    public static ServiceResult Ok() => new(true);
    public static ServiceResult Invalid(string error) => new(false, error);
}
public sealed record ServiceResult<T>(bool Success, T? Value, string? Error = null)
{
    public static ServiceResult<T> Ok(T value) => new(true, value);
    public static ServiceResult<T> Invalid(string error) => new(false, default, error);
    public static ServiceResult<T> Denied() => new(false, default, "Forbidden");
}
