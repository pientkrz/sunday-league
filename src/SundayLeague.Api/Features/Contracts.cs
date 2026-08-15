using SundayLeague.Api.Domain;

namespace SundayLeague.Api.Features;

public sealed record LeagueSummary(Guid Id, string Slug, string Name, int Tier, int PromotionPlaces, int RelegationPlaces, int PlayoffPlaces, int WinPoints, int DrawPoints, int LossPoints, StandingTiebreaker Tiebreaker, string SeasonName);
public sealed record StandingRow(int Position, Guid TeamId, string TeamName, int Played, int Won, int Drawn, int Lost, int GoalsFor, int GoalsAgainst, int Points)
{ public int GoalDifference => GoalsFor - GoalsAgainst; }
public sealed record PublicFixture(Guid Id, int RoundNumber, DateTimeOffset Kickoff, string HomeTeam, string AwayTeam, int? HomeScore, int? AwayScore, MatchStatus Status, Guid Version);
public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(string Email, string DisplayName, string Password, string InvitationToken);
public sealed record UpdateResultRequest(int HomeScore, int AwayScore, Guid Version);
public sealed record UpdateFixtureKickoffRequest(DateTimeOffset Kickoff, Guid Version);
public sealed record CancelFixtureRequest(Guid Version);
public sealed record CreateRoundRequest(DateTimeOffset Kickoff);
public sealed record CreateInvitationRequest(string Email, BoardRole Role, Guid? LeagueId);
public sealed record InvitationResponse(string Email, BoardRole Role, Guid? LeagueId, string? DevelopmentToken);
public sealed record BoardSession(string DisplayName, string Email, IReadOnlyList<MembershipSummary> Memberships);
public sealed record MembershipSummary(Guid? LeagueId, string? LeagueName, BoardRole Role);
public sealed record SeasonSummary(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn, SeasonStatus Status);
public sealed record CreateSeasonRequest(string Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record UpdateSeasonRequest(string Name, DateOnly StartsOn, DateOnly EndsOn, SeasonStatus Status);
public sealed record BoardLeagueConfiguration(Guid Id, string Slug, string Name, int Tier, int PromotionPlaces, int RelegationPlaces, int PlayoffPlaces, int WinPoints, int DrawPoints, int LossPoints, StandingTiebreaker Tiebreaker, bool IsPublished, Guid? SeasonId, string? SeasonName, IReadOnlyList<BoardTeam> Teams);
public sealed record BoardMemberSummary(Guid MembershipId, string DisplayName, string Email, Guid? LeagueId, string? LeagueName, BoardRole Role);
public sealed record BoardTeam(Guid Id, string Name, string ShortName, string? CrestUrl);
public sealed record CreateLeagueRequest(string Name, int PromotionPlaces, int RelegationPlaces, int PlayoffPlaces, int WinPoints, int DrawPoints, int LossPoints, StandingTiebreaker Tiebreaker, bool IsPublished);
public sealed record UpdateLeagueRequest(string Name, int PromotionPlaces, int RelegationPlaces, int PlayoffPlaces, int WinPoints, int DrawPoints, int LossPoints, StandingTiebreaker Tiebreaker, bool IsPublished);
public sealed record ReorderLeaguesRequest(IReadOnlyList<Guid> LeagueIds);
public sealed record CreateTeamRequest(string Name, string ShortName);
public sealed record UpdateTeamRequest(string Name, string ShortName, Guid LeagueId);
public sealed record CreateManualRoundRequest(DateTimeOffset Kickoff, IReadOnlyList<FixturePairRequest> Fixtures);
public sealed record FixturePairRequest(Guid HomeTeamId, Guid AwayTeamId);
