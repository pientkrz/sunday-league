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

public sealed class SeasonService(LeagueDbContext db)
{
    public async Task EnsureActiveSeasonAsync()
    {
        var active = await db.Seasons.SingleOrDefaultAsync(season => season.Status == SeasonStatus.Active);
        if (active is null)
        {
            var year = DateTime.UtcNow.Year;
            active = await db.Seasons.SingleOrDefaultAsync(season => season.Name == $"{year}/{(year + 1) % 100:D2}")
                ?? new Season { Name = $"{year}/{(year + 1) % 100:D2}", StartsOn = new DateOnly(year, 7, 1), EndsOn = new DateOnly(year + 1, 6, 30) };
            active.Status = SeasonStatus.Active;
            if (db.Entry(active).State == EntityState.Detached) db.Seasons.Add(active);
        }
        var legacyLeagues = await db.Leagues.Where(league => league.SeasonId == null).ToListAsync();
        foreach (var league in legacyLeagues) league.SeasonId = active.Id;
        if (legacyLeagues.Count > 0 || db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
    }

    public Task<bool> HasUnplayedFixturesAsync(Guid seasonId) => db.Matches.AnyAsync(match => match.League.SeasonId == seasonId && (match.Status == MatchStatus.Scheduled || match.Status == MatchStatus.Reported || match.Status == MatchStatus.Postponed));
}

public sealed class SeasonRolloverService(LeagueDbContext db, StandingsService standings, SeasonService seasons)
{
    public async Task<ServiceResult<SeasonRolloverSummary>> CreateAsync(ClaimsPrincipal actor, Guid sourceSeasonId, Guid destinationSeasonId, AuditService audit)
    {
        if (sourceSeasonId == destinationSeasonId) return ServiceResult<SeasonRolloverSummary>.Invalid("Choose a different draft season for the rollover.");
        var source = await db.Seasons.SingleOrDefaultAsync(season => season.Id == sourceSeasonId);
        var destination = await db.Seasons.SingleOrDefaultAsync(season => season.Id == destinationSeasonId);
        if (source is null || destination is null) return ServiceResult<SeasonRolloverSummary>.Invalid("The source or destination season was not found.");
        if (source.Status != SeasonStatus.Active || destination.Status != SeasonStatus.Draft || destination.StartsOn <= source.EndsOn)
            return ServiceResult<SeasonRolloverSummary>.Invalid("Roll over an active completed season into a later draft season.");
        if (await seasons.HasUnplayedFixturesAsync(source.Id)) return ServiceResult<SeasonRolloverSummary>.Invalid("All source fixtures must be confirmed or cancelled before rollover.");
        if (await db.Leagues.AnyAsync(league => league.SeasonId == destination.Id)) return ServiceResult<SeasonRolloverSummary>.Invalid("The draft season already contains leagues.");

        var sourceLeagues = await db.Leagues.Include(league => league.Teams).Where(league => league.SeasonId == source.Id).OrderBy(league => league.Tier).ToListAsync();
        if (sourceLeagues.Count == 0) return ServiceResult<SeasonRolloverSummary>.Invalid("The source season has no leagues to roll over.");
        var rankings = new Dictionary<Guid, IReadOnlyList<StandingRow>>();
        foreach (var league in sourceLeagues)
        {
            var table = await standings.GetAsync(league.Id);
            if (league.PromotionPlaces + league.RelegationPlaces > table.Count)
                return ServiceResult<SeasonRolloverSummary>.Invalid($"{league.Name} has overlapping promotion and relegation places.");
            rankings[league.Id] = table;
        }

        var teamDestinations = sourceLeagues.SelectMany(league => league.Teams).ToDictionary(team => team.Id, team => team.LeagueId);
        var promoted = 0;
        var relegated = 0;
        for (var index = 0; index < sourceLeagues.Count; index++)
        {
            var league = sourceLeagues[index];
            var table = rankings[league.Id];
            if (index > 0)
            {
                foreach (var team in table.Take(league.PromotionPlaces)) teamDestinations[team.TeamId] = sourceLeagues[index - 1].Id;
                promoted += Math.Min(league.PromotionPlaces, table.Count);
            }
            if (index < sourceLeagues.Count - 1)
            {
                foreach (var team in table.TakeLast(league.RelegationPlaces)) teamDestinations[team.TeamId] = sourceLeagues[index + 1].Id;
                relegated += Math.Min(league.RelegationPlaces, table.Count);
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var leagueMap = sourceLeagues.ToDictionary(league => league.Id, league => new League
        {
            SeasonId = destination.Id, Slug = league.Slug, Name = league.Name, Tier = league.Tier, PromotionPlaces = league.PromotionPlaces,
            RelegationPlaces = league.RelegationPlaces, PlayoffPlaces = league.PlayoffPlaces, WinPoints = league.WinPoints, DrawPoints = league.DrawPoints,
            LossPoints = league.LossPoints, Tiebreaker = league.Tiebreaker, IsPublished = false
        });
        db.Leagues.AddRange(leagueMap.Values);
        var teams = sourceLeagues.SelectMany(league => league.Teams).Select(team => new Team
        {
            League = leagueMap[teamDestinations[team.Id]], Name = team.Name, ShortName = team.ShortName, CrestUrl = team.CrestUrl
        }).ToList();
        db.Teams.AddRange(teams);
        var sourceLeagueIds = sourceLeagues.Select(league => league.Id).ToList();
        var scopedMemberships = await db.BoardMemberships.Where(membership => membership.LeagueId != null && sourceLeagueIds.Contains(membership.LeagueId.Value)).ToListAsync();
        foreach (var membership in scopedMemberships)
            db.BoardMemberships.Add(new BoardMembership { UserId = membership.UserId, LeagueId = leagueMap[membership.LeagueId!.Value].Id, Role = membership.Role });
        await audit.RecordAsync(actor, "season.rolled_over", "Season", destination.Id, $"{source.Name} => {destination.Name}; {promoted} promoted, {relegated} relegated.");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return ServiceResult<SeasonRolloverSummary>.Ok(new SeasonRolloverSummary(source.Id, destination.Id, leagueMap.Count, teams.Count, promoted, relegated));
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
