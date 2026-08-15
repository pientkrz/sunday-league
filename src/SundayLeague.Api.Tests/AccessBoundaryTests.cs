using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SundayLeague.Api.Data;
using SundayLeague.Api.Domain;
using SundayLeague.Api.Features;

namespace SundayLeague.Api.Tests;

public sealed class AccessBoundaryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    [Fact]
    public async Task Startup_applies_the_initial_schema_migration()
    {
        using var scope = factory.Services.CreateScope();
        var migrations = await scope.ServiceProvider.GetRequiredService<LeagueDbContext>().Database.GetAppliedMigrationsAsync();

        Assert.Contains(migrations, migration => migration.EndsWith("InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Published_standings_are_available_without_an_account()
    {
        var response = await _client.GetAsync("/api/public/leagues/premier-division/standings");

        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<StandingRow>>();
        Assert.NotNull(rows);
        Assert.Equal("Northside FC", rows![0].TeamName);
        Assert.Equal(3, rows[0].Points);
    }

    [Fact]
    public async Task Standings_apply_configured_points_and_tiebreaker_order()
    {
        const string slug = "rules-test-division";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
            var activeSeason = await db.Seasons.SingleAsync(season => season.Status == SeasonStatus.Active);
            var league = new League { Slug = slug, Name = "Rules Test Division", Tier = 99, IsPublished = true, Season = activeSeason, WinPoints = 1, DrawPoints = 1, LossPoints = 0, Tiebreaker = StandingTiebreaker.GoalDifferenceThenGoalsFor };
            var teams = new[] { "Alpha FC", "Bravo FC", "Charlie FC", "Delta FC" }.Select((name, index) => new Team { League = league, Name = name, ShortName = $"R{index}" }).ToArray();
            db.Leagues.Add(league);
            db.Teams.AddRange(teams);
            db.Matches.AddRange(
                new LeagueMatch { League = league, RoundNumber = 1, Kickoff = DateTimeOffset.UtcNow, HomeTeam = teams[0], AwayTeam = teams[1], HomeScore = 1, AwayScore = 0, Status = MatchStatus.Confirmed },
                new LeagueMatch { League = league, RoundNumber = 1, Kickoff = DateTimeOffset.UtcNow, HomeTeam = teams[2], AwayTeam = teams[3], HomeScore = 2, AwayScore = 2, Status = MatchStatus.Confirmed });
            await db.SaveChangesAsync();
        }

        var goalDifferenceFirst = await _client.GetFromJsonAsync<List<StandingRow>>($"/api/public/leagues/{slug}/standings");
        Assert.Equal("Alpha FC", goalDifferenceFirst![0].TeamName);
        Assert.Equal(1, goalDifferenceFirst[0].Points);

        using (var scope = factory.Services.CreateScope())
        {
            var league = await scope.ServiceProvider.GetRequiredService<LeagueDbContext>().Leagues.SingleAsync(item => item.Slug == slug);
            league.Tiebreaker = StandingTiebreaker.GoalsForThenGoalDifference;
            await scope.ServiceProvider.GetRequiredService<LeagueDbContext>().SaveChangesAsync();
        }
        var goalsForFirst = await _client.GetFromJsonAsync<List<StandingRow>>($"/api/public/leagues/{slug}/standings");

        Assert.Equal("Charlie FC", goalsForFirst![0].TeamName);
    }

    [Fact]
    public async Task Owner_can_create_a_draft_season_but_cannot_activate_it_early()
    {
        await CreateOwnerAsync("season-owner@example.com");
        await SignInAsync("season-owner@example.com", "TestPassword!42");
        var create = await SendWithCsrfAsync(HttpMethod.Post, "/api/board/seasons", new CreateSeasonRequest("2027/28", new DateOnly(2027, 7, 1), new DateOnly(2028, 6, 30)));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
        var season = await db.Seasons.SingleAsync(item => item.Name == "2027/28");

        var activate = await SendWithCsrfAsync(HttpMethod.Put, $"/api/board/seasons/{season.Id}", new UpdateSeasonRequest(season.Name, season.StartsOn, season.EndsOn, SeasonStatus.Active));

        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
    }

    [Fact]
    public async Task Owner_can_roll_completed_standings_into_a_draft_season_without_changing_history()
    {
        Season originalActive;
        Season source;
        Season destination;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
            originalActive = await db.Seasons.SingleAsync(item => item.Status == SeasonStatus.Active);
            originalActive.Status = SeasonStatus.Draft;
            source = new Season { Name = $"Rollover source {Guid.NewGuid():N}", StartsOn = new DateOnly(2027, 7, 1), EndsOn = new DateOnly(2028, 6, 30), Status = SeasonStatus.Active };
            destination = new Season { Name = $"Rollover destination {Guid.NewGuid():N}", StartsOn = new DateOnly(2028, 7, 1), EndsOn = new DateOnly(2029, 6, 30), Status = SeasonStatus.Draft };
            var premier = new League { Season = source, Slug = "rollover-premier", Name = "Rollover Premier", Tier = 1, RelegationPlaces = 1, IsPublished = true };
            var divisionOne = new League { Season = source, Slug = "rollover-division-one", Name = "Rollover Division One", Tier = 2, PromotionPlaces = 1, IsPublished = true };
            var alpha = new Team { League = premier, Name = "Rollover Alpha", ShortName = "RA" };
            var bravo = new Team { League = premier, Name = "Rollover Bravo", ShortName = "RB" };
            var charlie = new Team { League = divisionOne, Name = "Rollover Charlie", ShortName = "RC" };
            var delta = new Team { League = divisionOne, Name = "Rollover Delta", ShortName = "RD" };
            db.Seasons.AddRange(source, destination);
            db.Leagues.AddRange(premier, divisionOne);
            db.Teams.AddRange(alpha, bravo, charlie, delta);
            db.Matches.AddRange(
                new LeagueMatch { League = premier, RoundNumber = 1, Kickoff = DateTimeOffset.UtcNow, HomeTeam = alpha, AwayTeam = bravo, HomeScore = 2, AwayScore = 0, Status = MatchStatus.Confirmed },
                new LeagueMatch { League = divisionOne, RoundNumber = 1, Kickoff = DateTimeOffset.UtcNow, HomeTeam = charlie, AwayTeam = delta, HomeScore = 1, AwayScore = 0, Status = MatchStatus.Confirmed });
            await db.SaveChangesAsync();
        }
        await CreateOwnerAsync("rollover-owner@example.com");
        await SignInAsync("rollover-owner@example.com", "TestPassword!42");

        var response = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/seasons/{source.Id}/rollover/{destination.Id}", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var verification = factory.Services.CreateScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<LeagueDbContext>();
            var clonedLeagues = await db.Leagues.Include(item => item.Teams).Where(item => item.SeasonId == destination.Id).OrderBy(item => item.Tier).ToListAsync();
            Assert.Equal(2, clonedLeagues.Count);
            Assert.Equal(new[] { "Rollover Alpha", "Rollover Charlie" }, clonedLeagues[0].Teams.OrderBy(item => item.Name).Select(item => item.Name));
            Assert.Equal(new[] { "Rollover Bravo", "Rollover Delta" }, clonedLeagues[1].Teams.OrderBy(item => item.Name).Select(item => item.Name));
            Assert.All(clonedLeagues, item => Assert.False(item.IsPublished));
            Assert.Equal(2, await db.Leagues.CountAsync(item => item.SeasonId == source.Id));
            Assert.True(await db.AuditEvents.AnyAsync(item => item.Action == "season.rolled_over" && item.EntityId == destination.Id));
            originalActive = await db.Seasons.SingleAsync(item => item.Id == originalActive.Id);
            source = await db.Seasons.SingleAsync(item => item.Id == source.Id);
            originalActive.Status = SeasonStatus.Active;
            source.Status = SeasonStatus.Archived;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Anonymous_users_cannot_change_a_result()
    {
        var fixture = await GetFixtureAsync();

        var response = await _client.PutAsJsonAsync($"/api/board/matches/{fixture.Id}/result", new UpdateResultRequest(4, 0, fixture.Version));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Results_editor_can_update_scores_but_cannot_change_fixture_configuration()
    {
        var league = await CreateResultsEditorAsync();
        await SignInAsync("editor@example.com", "TestPassword!42");
        var fixture = await GetFixtureAsync();

        var resultResponse = await SendWithCsrfAsync(HttpMethod.Put, $"/api/board/matches/{fixture.Id}/result", new UpdateResultRequest(4, 0, fixture.Version));
        var schedulerResponse = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/leagues/{league.Id}/rounds/random", new CreateRoundRequest(DateTimeOffset.UtcNow.AddDays(7)));
        var teamResponse = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/leagues/{league.Id}/teams", new CreateTeamRequest("Unauthorised FC", "UFC"));
        var crestResponse = await SendCrestWithCsrfAsync(await GetTeamIdAsync(league.Id), "not-a-crest");
        var memberListResponse = await _client.GetAsync("/api/board/members");
        var auditListResponse = await _client.GetAsync("/api/board/audit-events");
        var rolloverResponse = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/seasons/{Guid.NewGuid()}/rollover/{Guid.NewGuid()}", new { });
        var rescheduleResponse = await SendWithCsrfAsync(HttpMethod.Put, $"/api/board/matches/{fixture.Id}/kickoff", new UpdateFixtureKickoffRequest(DateTimeOffset.UtcNow.AddDays(8), fixture.Version));
        var postponeResponse = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/matches/{fixture.Id}/postpone", new CancelFixtureRequest(fixture.Version));
        var cancelResponse = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/matches/{fixture.Id}/cancel", new CancelFixtureRequest(fixture.Version));
        var teamRemovalResponse = await SendWithCsrfAsync(HttpMethod.Delete, $"/api/board/teams/{await GetTeamIdAsync(league.Id)}", new { });

        Assert.Equal(HttpStatusCode.NoContent, resultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, schedulerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, teamResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crestResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberListResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, auditListResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rolloverResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rescheduleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postponeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, teamRemovalResponse.StatusCode);
        using var verification = factory.Services.CreateScope();
        var reported = await verification.ServiceProvider.GetRequiredService<LeagueDbContext>().Matches.SingleAsync(match => match.Id == fixture.Id);
        Assert.Equal(MatchStatus.Reported, reported.Status);
    }

    [Fact]
    public async Task Owner_can_add_a_team_and_create_a_random_round()
    {
        var league = await CreateOwnerAsync();
        await SignInAsync("owner-test@example.com", "TestPassword!42");

        var addTeam = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/leagues/{league.Id}/teams", new CreateTeamRequest("New Town FC", "NTFC"));
        var round = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/leagues/{league.Id}/rounds/random", new CreateRoundRequest(DateTimeOffset.UtcNow.AddDays(7)));

        Assert.Equal(HttpStatusCode.Created, addTeam.StatusCode);
        Assert.Equal(HttpStatusCode.Created, round.StatusCode);
    }

    [Fact]
    public async Task Owner_can_create_a_complete_manual_round()
    {
        var league = await CreateOwnerAsync("manual-owner@example.com");
        await SignInAsync("manual-owner@example.com", "TestPassword!42");
        using var scope = factory.Services.CreateScope();
        var teams = await scope.ServiceProvider.GetRequiredService<LeagueDbContext>().Teams.Where(team => team.LeagueId == league.Id).OrderBy(team => team.Name).ToListAsync();
        var request = new CreateManualRoundRequest(DateTimeOffset.UtcNow.AddDays(7),
        [new FixturePairRequest(teams[0].Id, teams[1].Id), new FixturePairRequest(teams[2].Id, teams[3].Id)]);

        var response = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/leagues/{league.Id}/rounds/manual", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Owner_can_generate_a_complete_round_robin_schedule_without_duplicate_pairings()
    {
        League league;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
            var season = await db.Seasons.SingleAsync(item => item.Status == SeasonStatus.Active);
            league = new League { Slug = "schedule-test", Name = "Schedule Test", Tier = 50, Season = season };
            db.Leagues.Add(league);
            db.Teams.AddRange(Enumerable.Range(1, 4).Select(index => new Team { League = league, Name = $"Schedule {index}", ShortName = $"S{index}" }));
            await db.SaveChangesAsync();
        }
        await CreateOwnerAsync("schedule-owner@example.com");
        await SignInAsync("schedule-owner@example.com", "TestPassword!42");

        var response = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/leagues/{league.Id}/schedule/round-robin", new CreateSeasonScheduleRequest(DateTimeOffset.UtcNow.AddDays(7), 7, false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var verification = factory.Services.CreateScope();
        var matches = await verification.ServiceProvider.GetRequiredService<LeagueDbContext>().Matches.Where(match => match.LeagueId == league.Id).ToListAsync();
        Assert.Equal(6, matches.Count);
        Assert.Equal(3, matches.Select(match => match.RoundNumber).Distinct().Count());
        Assert.All(matches.GroupBy(match => match.RoundNumber), round => Assert.Equal(2, round.Count()));
        Assert.Equal(6, matches.Select(match => string.Join('-', new[] { match.HomeTeamId, match.AwayTeamId }.Order())).Distinct().Count());
    }

    [Fact]
    public async Task Owner_can_review_and_revoke_non_owner_membership()
    {
        await CreateResultsEditorAsync("members-editor@example.com");
        await CreateOwnerAsync("membership-owner@example.com");
        await SignInAsync("membership-owner@example.com", "TestPassword!42");

        var listResponse = await _client.GetAsync("/api/board/members");
        listResponse.EnsureSuccessStatusCode();
        var members = await listResponse.Content.ReadFromJsonAsync<List<BoardMemberSummary>>();
        var editor = Assert.Single(members!, member => member.Email == "members-editor@example.com");
        var revokeResponse = await SendWithCsrfAsync(HttpMethod.Delete, $"/api/board/members/{editor.MembershipId}", new { });

        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
        Assert.False(await db.BoardMemberships.AnyAsync(membership => membership.Id == editor.MembershipId));
        Assert.True(await db.AuditEvents.AnyAsync(item => item.EntityId == editor.MembershipId && item.Action == "membership.revoked"));
    }

    [Fact]
    public async Task Owner_can_view_recent_audit_events()
    {
        var league = await CreateOwnerAsync("audit-owner@example.com");
        await SignInAsync("audit-owner@example.com", "TestPassword!42");
        var createTeam = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/leagues/{league.Id}/teams", new CreateTeamRequest("Audit Trail FC", "ATFC"));
        Assert.Equal(HttpStatusCode.Created, createTeam.StatusCode);

        var response = await _client.GetAsync("/api/board/audit-events");

        response.EnsureSuccessStatusCode();
        var events = await response.Content.ReadFromJsonAsync<List<AuditEventSummary>>();
        var teamCreated = Assert.Single(events!, item => item.Action == "team.created" && item.Detail == "Audit Trail FC");
        Assert.Equal("Test Owner", teamCreated.ActorDisplayName);
    }

    [Fact]
    public async Task Owner_can_reorder_the_complete_league_hierarchy_with_an_audit_record()
    {
        await CreateOwnerAsync("hierarchy-owner@example.com");
        await SignInAsync("hierarchy-owner@example.com", "TestPassword!42");
        using var beforeScope = factory.Services.CreateScope();
        var db = beforeScope.ServiceProvider.GetRequiredService<LeagueDbContext>();
        var initialOrder = await db.Leagues.OrderBy(league => league.Tier).Select(league => league.Id).ToListAsync();
        Assert.NotEmpty(initialOrder);

        var response = await SendWithCsrfAsync(HttpMethod.Put, "/api/board/leagues/hierarchy", new ReorderLeaguesRequest(initialOrder.AsEnumerable().Reverse().ToList()));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var verification = factory.Services.CreateScope();
        var verificationDb = verification.ServiceProvider.GetRequiredService<LeagueDbContext>();
        var reordered = await verificationDb.Leagues.OrderBy(league => league.Tier).Select(league => league.Id).ToListAsync();
        Assert.Equal(initialOrder.AsEnumerable().Reverse(), reordered);
        Assert.True(await verificationDb.AuditEvents.AnyAsync(item => item.Action == "league.hierarchy.updated"));

        var restoreResponse = await SendWithCsrfAsync(HttpMethod.Put, "/api/board/leagues/hierarchy", new ReorderLeaguesRequest(initialOrder));
        Assert.Equal(HttpStatusCode.NoContent, restoreResponse.StatusCode);
    }

    [Fact]
    public async Task Owner_can_move_and_remove_a_team_without_fixtures_with_audit_records()
    {
        League sourceLeague;
        League destinationLeague;
        Team team;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
            var season = await db.Seasons.SingleAsync(item => item.Status == SeasonStatus.Active);
            sourceLeague = await db.Leagues.OrderBy(league => league.Tier).FirstAsync();
            destinationLeague = new League { Name = "Team Move Test", Slug = $"team-move-{Guid.NewGuid():N}", Tier = (await db.Leagues.MaxAsync(league => league.Tier)) + 1, Season = season };
            team = new Team { League = sourceLeague, Name = "Movable Town", ShortName = "MOVE" };
            db.Leagues.Add(destinationLeague);
            db.Teams.Add(team);
            await db.SaveChangesAsync();
        }
        await CreateOwnerAsync("team-owner@example.com");
        await SignInAsync("team-owner@example.com", "TestPassword!42");

        var moveResponse = await SendWithCsrfAsync(HttpMethod.Put, $"/api/board/teams/{team.Id}", new UpdateTeamRequest(team.Name, team.ShortName, destinationLeague.Id));
        Assert.Equal(HttpStatusCode.NoContent, moveResponse.StatusCode);
        var removeResponse = await SendWithCsrfAsync(HttpMethod.Delete, $"/api/board/teams/{team.Id}", new { });

        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        using var verification = factory.Services.CreateScope();
        var verificationDb = verification.ServiceProvider.GetRequiredService<LeagueDbContext>();
        Assert.False(await verificationDb.Teams.AnyAsync(item => item.Id == team.Id));
        var auditEvents = await verificationDb.AuditEvents.Where(item => item.EntityId == team.Id).Select(item => item.Action).ToListAsync();
        Assert.Contains("team.updated", auditEvents);
        Assert.Contains("team.deleted", auditEvents);
    }

    [Fact]
    public async Task Owner_can_postpone_reschedule_and_cancel_an_unplayed_fixture_with_audit_records()
    {
        var league = await CreateOwnerAsync("fixture-owner@example.com");
        await SignInAsync("fixture-owner@example.com", "TestPassword!42");
        var createRound = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/leagues/{league.Id}/rounds/random", new CreateRoundRequest(DateTimeOffset.UtcNow.AddDays(7)));
        Assert.Equal(HttpStatusCode.Created, createRound.StatusCode);
        var scheduled = await GetScheduledFixtureAsync(league.Id);
        var postponement = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/matches/{scheduled.Id}/postpone", new CancelFixtureRequest(scheduled.Version));
        Assert.Equal(HttpStatusCode.NoContent, postponement.StatusCode);
        var postponed = await GetFixtureByIdAsync(scheduled.Id);
        var resultWhilePostponed = await SendWithCsrfAsync(HttpMethod.Put, $"/api/board/matches/{scheduled.Id}/result", new UpdateResultRequest(1, 0, postponed.Version));
        Assert.Equal(MatchStatus.Postponed, postponed.Status);
        Assert.Equal(HttpStatusCode.Conflict, resultWhilePostponed.StatusCode);

        var newKickoff = DateTimeOffset.UtcNow.AddDays(9);

        var reschedule = await SendWithCsrfAsync(HttpMethod.Put, $"/api/board/matches/{scheduled.Id}/kickoff", new UpdateFixtureKickoffRequest(newKickoff, postponed.Version));
        Assert.Equal(HttpStatusCode.NoContent, reschedule.StatusCode);
        var rescheduled = await GetFixtureByIdAsync(scheduled.Id);
        Assert.Equal(MatchStatus.Scheduled, rescheduled.Status);

        var cancellation = await SendWithCsrfAsync(HttpMethod.Post, $"/api/board/matches/{scheduled.Id}/cancel", new CancelFixtureRequest(rescheduled.Version));
        Assert.Equal(HttpStatusCode.NoContent, cancellation.StatusCode);
        var cancelled = await GetFixtureByIdAsync(scheduled.Id);
        var resultAfterCancellation = await SendWithCsrfAsync(HttpMethod.Put, $"/api/board/matches/{scheduled.Id}/result", new UpdateResultRequest(1, 0, cancelled.Version));

        Assert.Equal(MatchStatus.Cancelled, cancelled.Status);
        Assert.Equal(HttpStatusCode.Conflict, resultAfterCancellation.StatusCode);
        using var scope = factory.Services.CreateScope();
        var auditEvents = await scope.ServiceProvider.GetRequiredService<LeagueDbContext>().AuditEvents.Where(item => item.EntityId == scheduled.Id).Select(item => item.Action).ToListAsync();
        Assert.Contains("match.postponed", auditEvents);
        Assert.Contains("match.kickoff.updated", auditEvents);
        Assert.Contains("match.cancelled", auditEvents);
    }

    private async Task<PublicFixture> GetFixtureAsync()
    {
        var response = await _client.GetFromJsonAsync<List<PublicFixture>>("/api/public/leagues/premier-division/fixtures");
        var confirmed = response!.Where(fixture => fixture.Status == MatchStatus.Confirmed).ToList();
        Assert.NotEmpty(confirmed);
        return confirmed[0];
    }

    private async Task<Guid> GetTeamIdAsync(Guid leagueId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LeagueDbContext>().Teams.Where(team => team.LeagueId == leagueId).Select(team => team.Id).FirstAsync();
    }

    private async Task<LeagueMatch> GetScheduledFixtureAsync(Guid leagueId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LeagueDbContext>().Matches.AsNoTracking().Where(match => match.LeagueId == leagueId && match.Status == MatchStatus.Scheduled).OrderByDescending(match => match.RoundNumber).FirstAsync();
    }

    private async Task<LeagueMatch> GetFixtureByIdAsync(Guid matchId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LeagueDbContext>().Matches.AsNoTracking().SingleAsync(match => match.Id == matchId);
    }

    private async Task<League> CreateResultsEditorAsync(string email = "editor@example.com")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var league = await db.Leagues.OrderBy(item => item.Tier).FirstAsync();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Result Editor" };
        var result = await users.CreateAsync(user, "TestPassword!42");
        Assert.True(result.Succeeded);
        db.BoardMemberships.Add(new BoardMembership { UserId = user.Id, LeagueId = league.Id, Role = BoardRole.ResultsEditor });
        await db.SaveChangesAsync();
        return league;
    }

    private async Task<League> CreateOwnerAsync(string email = "owner-test@example.com")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var league = await db.Leagues.OrderBy(item => item.Tier).FirstAsync();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Test Owner" };
        var result = await users.CreateAsync(user, "TestPassword!42");
        Assert.True(result.Succeeded);
        db.BoardMemberships.Add(new BoardMembership { UserId = user.Id, Role = BoardRole.Owner });
        await db.SaveChangesAsync();
        return league;
    }

    private async Task SignInAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendWithCsrfAsync(HttpMethod method, string url, object body)
    {
        var csrf = await _client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendCrestWithCsrfAsync(Guid teamId, string content)
    {
        var csrf = await _client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(content), "crest", "crest.png");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/board/teams/{teamId}/crest") { Content = form };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await _client.SendAsync(request);
    }

    private sealed record CsrfResponse(string Token);
}
