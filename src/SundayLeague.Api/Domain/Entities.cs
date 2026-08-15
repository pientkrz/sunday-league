using Microsoft.AspNetCore.Identity;

namespace SundayLeague.Api.Domain;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public ICollection<BoardMembership> Memberships { get; set; } = [];
}

public sealed class League
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int PromotionPlaces { get; set; }
    public int RelegationPlaces { get; set; }
    public int PlayoffPlaces { get; set; }
    public int WinPoints { get; set; } = 3;
    public int DrawPoints { get; set; } = 1;
    public int LossPoints { get; set; }
    public StandingTiebreaker Tiebreaker { get; set; } = StandingTiebreaker.GoalDifferenceThenGoalsFor;
    public bool IsPublished { get; set; }
    public ICollection<Team> Teams { get; set; } = [];
}

public sealed class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeagueId { get; set; }
    public League League { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string? CrestUrl { get; set; }
}

public sealed class LeagueMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeagueId { get; set; }
    public League League { get; set; } = null!;
    public int RoundNumber { get; set; }
    public DateTimeOffset Kickoff { get; set; }
    public Guid HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;
    public Guid AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public MatchStatus Status { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public enum MatchStatus { Scheduled, Reported, Confirmed, Postponed, Cancelled }
public enum StandingTiebreaker { GoalDifferenceThenGoalsFor, GoalsForThenGoalDifference }
public enum BoardRole { Owner, CompetitionAdmin, ResultsEditor, Viewer }

public sealed class BoardMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public Guid? LeagueId { get; set; }
    public League? League { get; set; }
    public BoardRole Role { get; set; }
}

public sealed class BoardInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public BoardRole Role { get; set; }
    public Guid? LeagueId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; set; }
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Detail { get; set; } = string.Empty;
}
