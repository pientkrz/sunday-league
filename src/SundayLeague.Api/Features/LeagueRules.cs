using SundayLeague.Api.Domain;

namespace SundayLeague.Api.Features;

public static class LeagueRules
{
    public static bool HasValidScoring(int winPoints, int drawPoints, int lossPoints, StandingTiebreaker tiebreaker) =>
        winPoints is >= 0 and <= 10 && drawPoints is >= 0 and <= 10 && lossPoints is >= 0 and <= 10 &&
        winPoints >= drawPoints && drawPoints >= lossPoints && Enum.IsDefined(tiebreaker);
}
