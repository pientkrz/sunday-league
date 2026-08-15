using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading.RateLimiting;
using SundayLeague.Api.Auth;
using SundayLeague.Api.Data;
using SundayLeague.Api.Domain;
using SundayLeague.Api.Features;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=sunday-league.db";
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];

builder.Services.AddDbContext<LeagueDbContext>(options =>
{
    if (connectionString.StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString);
});
if (!string.IsNullOrWhiteSpace(keyRingPath))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyRingPath)).SetApplicationName("SundayLeague");
else
    builder.Services.AddDataProtection().SetApplicationName("SundayLeague");

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<LeagueDbContext>()
    .AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-sundayleague";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-sundayleague-xsrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddScoped<BoardAuthorizationService>();
builder.Services.AddScoped<StandingsService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<InitialOwnerService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("public", limiter =>
{
    limiter.PermitLimit = 120;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));

var app = builder.Build();
app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
}));
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
app.UseRateLimiter();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
    await db.Database.MigrateAsync();
    await LeagueSeed.EnsureSeededAsync(db);
    await scope.ServiceProvider.GetRequiredService<InitialOwnerService>().EnsureCreatedAsync();
}

var publicApi = app.MapGroup("/api/public").RequireRateLimiting("public");
publicApi.MapGet("/leagues", async (LeagueDbContext db) =>
    await db.Leagues.Where(league => league.IsPublished)
        .OrderBy(league => league.Tier)
        .Select(league => new LeagueSummary(league.Id, league.Slug, league.Name, league.Tier, league.PromotionPlaces, league.RelegationPlaces, league.PlayoffPlaces, league.WinPoints, league.DrawPoints, league.LossPoints, league.Tiebreaker))
        .ToListAsync());
publicApi.MapGet("/leagues/{slug}/standings", async (string slug, LeagueDbContext db, StandingsService standings) =>
{
    var league = await db.Leagues.SingleOrDefaultAsync(item => item.Slug == slug && item.IsPublished);
    return league is null ? Results.NotFound() : Results.Ok(await standings.GetAsync(league.Id));
});
publicApi.MapGet("/leagues/{slug}/fixtures", async (string slug, LeagueDbContext db) =>
{
    var league = await db.Leagues.SingleOrDefaultAsync(item => item.Slug == slug && item.IsPublished);
    if (league is null) return Results.NotFound();
    return Results.Ok(await db.Matches.Where(match => match.LeagueId == league.Id)
        .OrderBy(match => match.RoundNumber).ThenBy(match => match.Id).Select(match => new PublicFixture(match.Id, match.RoundNumber, match.Kickoff, match.HomeTeam.Name, match.AwayTeam.Name, match.HomeScore, match.AwayScore, match.Status, match.Version)).ToListAsync());
});

var auth = app.MapGroup("/api/auth");
auth.MapPost("/login", async (LoginRequest request, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> users) =>
{
    var user = await users.FindByEmailAsync(request.Email.Trim());
    if (user is null) return Results.Unauthorized();
    var result = await signInManager.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: true);
    return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
});
auth.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) => { await signInManager.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
auth.MapPost("/register", async (RegisterRequest request, InvitationService invitations) =>
{
    var result = await invitations.AcceptAsync(request);
    return result.Success ? Results.NoContent() : Results.BadRequest(new { error = result.Error });
});
auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
    Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));
auth.MapGet("/session", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users, LeagueDbContext db) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    var user = userId is null ? null : await users.FindByIdAsync(userId);
    if (user is null) return Results.Unauthorized();
    var memberships = await db.BoardMemberships.Where(membership => membership.UserId == user.Id)
        .Select(membership => new MembershipSummary(membership.LeagueId, membership.League == null ? null : membership.League.Name, membership.Role))
        .ToListAsync();
    return Results.Ok(new BoardSession(user.DisplayName, user.Email!, memberships));
}).RequireAuthorization();

var board = app.MapGroup("/api/board").RequireAuthorization();
board.AddEndpointFilter(async (context, next) =>
{
    if (HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method) || HttpMethods.IsOptions(context.HttpContext.Request.Method))
        return await next(context);
    try
    {
        await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context.HttpContext);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest(new { error = "The anti-forgery token is missing or invalid." });
    }
    return await next(context);
});
board.MapGet("/leagues", async (ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access) =>
{
    if (!await access.IsBoardMemberAsync(user)) return Results.Forbid();
    var leagues = await db.Leagues.OrderBy(league => league.Tier).Select(league => new BoardLeagueConfiguration(
        league.Id, league.Slug, league.Name, league.Tier, league.PromotionPlaces, league.RelegationPlaces, league.PlayoffPlaces, league.WinPoints, league.DrawPoints, league.LossPoints, league.Tiebreaker, league.IsPublished,
        league.Teams.OrderBy(team => team.Name).Select(team => new BoardTeam(team.Id, team.Name, team.ShortName, team.CrestUrl)).ToList())).ToListAsync();
    return Results.Ok(leagues);
});
board.MapPost("/leagues", async (CreateLeagueRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    if (!await access.IsOwnerAsync(user)) return Results.Forbid();
    var slug = LeagueSlug.From(request.Name);
    if (string.IsNullOrWhiteSpace(slug) || request.Name.Length > 120 || request.PromotionPlaces < 0 || request.RelegationPlaces < 0 || request.PlayoffPlaces < 0 || !LeagueRules.HasValidScoring(request.WinPoints, request.DrawPoints, request.LossPoints, request.Tiebreaker))
        return Results.BadRequest(new { error = "Provide a valid league configuration." });
    if (await db.Leagues.AnyAsync(league => league.Slug == slug)) return Results.Conflict(new { error = "A league with this name already exists." });
    var tier = (await db.Leagues.Select(league => (int?)league.Tier).MaxAsync() ?? 0) + 1;
    var league = new League { Name = request.Name.Trim(), Slug = slug, Tier = tier, PromotionPlaces = request.PromotionPlaces, RelegationPlaces = request.RelegationPlaces, PlayoffPlaces = request.PlayoffPlaces, WinPoints = request.WinPoints, DrawPoints = request.DrawPoints, LossPoints = request.LossPoints, Tiebreaker = request.Tiebreaker, IsPublished = request.IsPublished };
    db.Leagues.Add(league);
    await audit.RecordAsync(user, "league.created", "League", league.Id, league.Name);
    await db.SaveChangesAsync();
    return Results.Created($"/api/board/leagues/{league.Id}", new { league.Id });
});
board.MapPut("/leagues/{leagueId:guid}", async (Guid leagueId, UpdateLeagueRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    var league = await db.Leagues.SingleOrDefaultAsync(item => item.Id == leagueId);
    if (league is null) return Results.NotFound();
    if (!await access.CanConfigureAsync(user, leagueId)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || request.PromotionPlaces < 0 || request.RelegationPlaces < 0 || request.PlayoffPlaces < 0 || !LeagueRules.HasValidScoring(request.WinPoints, request.DrawPoints, request.LossPoints, request.Tiebreaker))
        return Results.BadRequest(new { error = "Provide a valid league configuration." });
    league.Name = request.Name.Trim(); league.PromotionPlaces = request.PromotionPlaces; league.RelegationPlaces = request.RelegationPlaces; league.PlayoffPlaces = request.PlayoffPlaces; league.WinPoints = request.WinPoints; league.DrawPoints = request.DrawPoints; league.LossPoints = request.LossPoints; league.Tiebreaker = request.Tiebreaker; league.IsPublished = request.IsPublished;
    await audit.RecordAsync(user, "league.updated", "League", league.Id, league.Name);
    await db.SaveChangesAsync();
    return Results.NoContent();
});
board.MapPut("/leagues/hierarchy", async (ReorderLeaguesRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    if (!await access.IsOwnerAsync(user)) return Results.Forbid();
    var leagues = await db.Leagues.ToListAsync();
    if (request.LeagueIds.Count != leagues.Count || request.LeagueIds.Distinct().Count() != leagues.Count || request.LeagueIds.Except(leagues.Select(league => league.Id)).Any())
        return Results.BadRequest(new { error = "The hierarchy must include each league exactly once." });
    for (var index = 0; index < request.LeagueIds.Count; index++) leagues.Single(league => league.Id == request.LeagueIds[index]).Tier = index + 1;
    await audit.RecordAsync(user, "league.hierarchy.updated", "LeagueHierarchy", Guid.Empty, "League tiers reordered.");
    await db.SaveChangesAsync();
    return Results.NoContent();
});
board.MapPost("/leagues/{leagueId:guid}/teams", async (Guid leagueId, CreateTeamRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    if (!await access.CanConfigureAsync(user, leagueId)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || string.IsNullOrWhiteSpace(request.ShortName) || request.ShortName.Trim().Length > 5)
        return Results.BadRequest(new { error = "Provide a team name and a short name of up to five characters." });
    if (!await db.Leagues.AnyAsync(league => league.Id == leagueId)) return Results.NotFound();
    if (await db.Teams.AnyAsync(team => team.LeagueId == leagueId && team.Name == request.Name.Trim())) return Results.Conflict(new { error = "This team already exists in the league." });
    var team = new Team { LeagueId = leagueId, Name = request.Name.Trim(), ShortName = request.ShortName.Trim().ToUpperInvariant() };
    db.Teams.Add(team);
    await audit.RecordAsync(user, "team.created", "Team", team.Id, team.Name);
    await db.SaveChangesAsync();
    return Results.Created($"/api/board/teams/{team.Id}", new { team.Id });
});
board.MapPut("/teams/{teamId:guid}", async (Guid teamId, UpdateTeamRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    var team = await db.Teams.SingleOrDefaultAsync(item => item.Id == teamId);
    if (team is null) return Results.NotFound();
    if (!await access.CanConfigureAsync(user, team.LeagueId) || !await access.CanConfigureAsync(user, request.LeagueId)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || string.IsNullOrWhiteSpace(request.ShortName) || request.ShortName.Trim().Length > 5 || !await db.Leagues.AnyAsync(league => league.Id == request.LeagueId))
        return Results.BadRequest(new { error = "Provide valid team details and league." });
    team.Name = request.Name.Trim(); team.ShortName = request.ShortName.Trim().ToUpperInvariant(); team.LeagueId = request.LeagueId;
    await audit.RecordAsync(user, "team.updated", "Team", team.Id, team.Name);
    await db.SaveChangesAsync();
    return Results.NoContent();
});
board.MapPost("/teams/{teamId:guid}/crest", async (Guid teamId, IFormFile crest, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit, IWebHostEnvironment environment) =>
{
    var team = await db.Teams.SingleOrDefaultAsync(item => item.Id == teamId);
    if (team is null) return Results.NotFound();
    if (!await access.CanConfigureAsync(user, team.LeagueId)) return Results.Forbid();
    if (!await CrestValidation.IsSafeSquarePngAsync(crest))
        return Results.BadRequest(new { error = "Upload a valid PNG crest at exactly 256 × 256 px and no more than 1 MB." });
    var directory = Path.Combine(environment.ContentRootPath, "wwwroot", "crests");
    Directory.CreateDirectory(directory);
    var fileName = $"{Guid.NewGuid():N}.png";
    await using (var destination = File.Create(Path.Combine(directory, fileName))) await crest.CopyToAsync(destination);
    team.CrestUrl = $"/crests/{fileName}";
    await audit.RecordAsync(user, "team.crest.uploaded", "Team", team.Id, team.CrestUrl);
    await db.SaveChangesAsync();
    return Results.Ok(new { team.CrestUrl });
});
board.MapPut("/matches/{matchId:guid}/result", async (Guid matchId, UpdateResultRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    if (request.HomeScore is < 0 or > 99 || request.AwayScore is < 0 or > 99)
        return Results.BadRequest(new { error = "Scores must be between 0 and 99." });
    var match = await db.Matches.SingleOrDefaultAsync(item => item.Id == matchId);
    if (match is null) return Results.NotFound();
    if (!await access.CanEditResultsAsync(user, match.LeagueId)) return Results.Forbid();
    if (match.Version != request.Version) return Results.Conflict(new { error = "This match has changed. Refresh and try again." });
    if (match.Status == MatchStatus.Cancelled) return Results.Conflict(new { error = "A cancelled fixture cannot receive a result." });
    var oldResult = $"{match.HomeScore}-{match.AwayScore}";
    match.HomeScore = request.HomeScore;
    match.AwayScore = request.AwayScore;
    match.Status = MatchStatus.Confirmed;
    match.Version = Guid.NewGuid();
    await audit.RecordAsync(user, "match.result.updated", "Match", match.Id, $"{oldResult} => {match.HomeScore}-{match.AwayScore}");
    await db.SaveChangesAsync();
    return Results.NoContent();
});
board.MapPut("/matches/{matchId:guid}/kickoff", async (Guid matchId, UpdateFixtureKickoffRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    var now = DateTimeOffset.UtcNow;
    if (request.Kickoff < now.AddYears(-1) || request.Kickoff > now.AddYears(10))
        return Results.BadRequest(new { error = "Choose a valid kick-off date and time." });
    var match = await db.Matches.SingleOrDefaultAsync(item => item.Id == matchId);
    if (match is null) return Results.NotFound();
    if (!await access.CanConfigureAsync(user, match.LeagueId)) return Results.Forbid();
    if (match.Version != request.Version) return Results.Conflict(new { error = "This match has changed. Refresh and try again." });
    if (match.Status is MatchStatus.Confirmed or MatchStatus.Cancelled)
        return Results.Conflict(new { error = "Only unplayed fixtures can be rescheduled." });
    var oldKickoff = match.Kickoff;
    match.Kickoff = request.Kickoff;
    match.Version = Guid.NewGuid();
    await audit.RecordAsync(user, "match.kickoff.updated", "Match", match.Id, $"{oldKickoff:O} => {match.Kickoff:O}");
    await db.SaveChangesAsync();
    return Results.NoContent();
});
board.MapPost("/matches/{matchId:guid}/cancel", async (Guid matchId, CancelFixtureRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    var match = await db.Matches.SingleOrDefaultAsync(item => item.Id == matchId);
    if (match is null) return Results.NotFound();
    if (!await access.CanConfigureAsync(user, match.LeagueId)) return Results.Forbid();
    if (match.Version != request.Version) return Results.Conflict(new { error = "This match has changed. Refresh and try again." });
    if (match.Status is not (MatchStatus.Scheduled or MatchStatus.Postponed))
        return Results.Conflict(new { error = "Only scheduled or postponed fixtures can be cancelled." });
    match.Status = MatchStatus.Cancelled;
    match.Version = Guid.NewGuid();
    await audit.RecordAsync(user, "match.cancelled", "Match", match.Id, $"Round {match.RoundNumber} fixture cancelled.");
    await db.SaveChangesAsync();
    return Results.NoContent();
});
board.MapPost("/leagues/{leagueId:guid}/rounds/random", async (Guid leagueId, CreateRoundRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    if (!await access.CanConfigureAsync(user, leagueId)) return Results.Forbid();
    var teamIds = await db.Teams.Where(team => team.LeagueId == leagueId).Select(team => team.Id).ToListAsync();
    if (teamIds.Count < 2) return Results.BadRequest(new { error = "A league needs at least two teams." });
    Random.Shared.Shuffle(CollectionsMarshal.AsSpan(teamIds));
    var nextRound = (await db.Matches.Where(match => match.LeagueId == leagueId).Select(match => (int?)match.RoundNumber).MaxAsync() ?? 0) + 1;
    for (var index = 0; index + 1 < teamIds.Count; index += 2)
        db.Matches.Add(new LeagueMatch { LeagueId = leagueId, RoundNumber = nextRound, Kickoff = request.Kickoff, HomeTeamId = teamIds[index], AwayTeamId = teamIds[index + 1], Status = MatchStatus.Scheduled });
    await audit.RecordAsync(user, "round.created", "League", leagueId, $"Round {nextRound} created.");
    await db.SaveChangesAsync();
    return Results.Created($"/api/board/leagues/{leagueId}/rounds/{nextRound}", new { round = nextRound });
});
board.MapPost("/leagues/{leagueId:guid}/rounds/manual", async (Guid leagueId, CreateManualRoundRequest request, ClaimsPrincipal user, LeagueDbContext db, BoardAuthorizationService access, AuditService audit) =>
{
    if (!await access.CanConfigureAsync(user, leagueId)) return Results.Forbid();
    var leagueTeamIds = await db.Teams.Where(team => team.LeagueId == leagueId).Select(team => team.Id).ToListAsync();
    var selectedIds = request.Fixtures.SelectMany(fixture => new[] { fixture.HomeTeamId, fixture.AwayTeamId }).ToList();
    var expectedTeams = leagueTeamIds.Count % 2 == 0 ? leagueTeamIds.Count : leagueTeamIds.Count - 1;
    if (request.Kickoff <= DateTimeOffset.UtcNow.AddYears(-1) || request.Fixtures.Count == 0 || selectedIds.Count != expectedTeams || selectedIds.Distinct().Count() != selectedIds.Count || selectedIds.Any(id => !leagueTeamIds.Contains(id)) || request.Fixtures.Any(fixture => fixture.HomeTeamId == fixture.AwayTeamId))
        return Results.BadRequest(new { error = "Every team must play once; an odd-sized league has one bye." });
    var round = (await db.Matches.Where(match => match.LeagueId == leagueId).Select(match => (int?)match.RoundNumber).MaxAsync() ?? 0) + 1;
    foreach (var fixture in request.Fixtures) db.Matches.Add(new LeagueMatch { LeagueId = leagueId, RoundNumber = round, Kickoff = request.Kickoff, HomeTeamId = fixture.HomeTeamId, AwayTeamId = fixture.AwayTeamId, Status = MatchStatus.Scheduled });
    await audit.RecordAsync(user, "round.manual.created", "League", leagueId, $"Round {round} created manually.");
    await db.SaveChangesAsync();
    return Results.Created($"/api/board/leagues/{leagueId}/rounds/{round}", new { round });
});
board.MapPost("/invitations", async (CreateInvitationRequest request, ClaimsPrincipal user, InvitationService invitations) =>
{
    var result = await invitations.CreateAsync(user, request);
    return result.Success ? Results.Created("/api/board/invitations", result.Value) : Results.Forbid();
});

app.Run();

public partial class Program;

static class CrestValidation
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static async Task<bool> IsSafeSquarePngAsync(IFormFile crest)
    {
        if (crest.Length is < 57 or > 1_048_576 || !string.Equals(crest.ContentType, "image/png", StringComparison.OrdinalIgnoreCase)) return false;
        await using var stream = crest.OpenReadStream();
        var header = new byte[24];
        try { await stream.ReadExactlyAsync(header); }
        catch (EndOfStreamException) { return false; }
        var width = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
        return header.AsSpan(0, 8).SequenceEqual(PngSignature) && header.AsSpan(12, 4).SequenceEqual("IHDR"u8) && width == 256 && height == 256;
    }
}
