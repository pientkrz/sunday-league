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

        Assert.Equal(HttpStatusCode.NoContent, resultResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, schedulerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, teamResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crestResponse.StatusCode);
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

    private async Task<League> CreateResultsEditorAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeagueDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var league = await db.Leagues.SingleAsync();
        var user = new ApplicationUser { UserName = "editor@example.com", Email = "editor@example.com", EmailConfirmed = true, DisplayName = "Result Editor" };
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
        var league = await db.Leagues.SingleAsync();
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
