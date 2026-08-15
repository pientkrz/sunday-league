using Microsoft.EntityFrameworkCore;
using SundayLeague.Api.Domain;

namespace SundayLeague.Api.Data;

public static class LeagueSeed
{
    public static async Task EnsureSeededAsync(LeagueDbContext db)
    {
        if (await db.Leagues.AnyAsync()) return;
        var premier = new League { Slug = "premier-division", Name = "Premier Division", Tier = 1, RelegationPlaces = 2, PlayoffPlaces = 2, IsPublished = true };
        db.Leagues.Add(premier);
        var teams = new[] { "Northside FC", "Riverside Rovers", "Old Town Athletic", "Park United" }
            .Select((name, index) => new Team { League = premier, Name = name, ShortName = $"T{index + 1}" }).ToArray();
        db.Teams.AddRange(teams);
        db.Matches.AddRange(
            new LeagueMatch { League = premier, RoundNumber = 1, Kickoff = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero), HomeTeam = teams[0], AwayTeam = teams[1], HomeScore = 2, AwayScore = 1, Status = MatchStatus.Confirmed },
            new LeagueMatch { League = premier, RoundNumber = 1, Kickoff = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero), HomeTeam = teams[2], AwayTeam = teams[3], HomeScore = 0, AwayScore = 0, Status = MatchStatus.Confirmed });
        await db.SaveChangesAsync();
    }
}
