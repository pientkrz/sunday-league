import { leagues, seasons, teams as allTeams, matches as allMatches } from './mockData';

// In a real app, these would be API calls.
// We'll add a little delay to simulate network latency.
const fakeFetch = (data) => new Promise(resolve => setTimeout(() => resolve(data), 200));

export const getLeagues = () => fakeFetch(leagues);

export const getLeague = (leagueId) => fakeFetch(leagues.find(l => l.id === leagueId));

export const getSeasonsForLeague = (leagueId) => {
  const leagueSeasons = seasons.filter(s => s.leagueId === leagueId);
  return fakeFetch(leagueSeasons);
};

export const getSeason = (seasonId) => {
    const season = seasons.find(s => s.id === seasonId);
    if (!season) return fakeFetch(null);

    const seasonTeams = allTeams.filter(t => season.teams.includes(t.id));
    const seasonMatches = allMatches.filter(m => m.seasonId === seasonId);

    return fakeFetch({
        ...season,
        teams: seasonTeams,
        matches: seasonMatches,
    });
};

export const getTeam = (teamId) => {
    const team = allTeams.find(t => t.id === teamId);
    if (!team) return fakeFetch(null);

    const teamMatches = allMatches.filter(m => m.homeTeamId === teamId || m.awayTeamId === teamId);
    return fakeFetch({ ...team, matches: teamMatches });
}

export const getAllTeams = () => fakeFetch(allTeams);

// This would be a POST/PUT request in a real app
export const saveMatches = (newMatches) => {
    allMatches.push(...newMatches);
    return fakeFetch({ success: true, count: newMatches.length });
};

// This would be a PUT/PATCH request in a real app
export const updateMatch = (matchId, newScores) => {
    const matchIndex = allMatches.findIndex(m => m.id === matchId);
    if (matchIndex === -1) {
        return fakeFetch({ success: false, error: 'Match not found' });
    }
    const updatedMatch = {
        ...allMatches[matchIndex],
        homeScore: newScores.homeScore,
        awayScore: newScores.awayScore,
        status: 'finished',
    };
    allMatches[matchIndex] = updatedMatch;
    return fakeFetch({ success: true, match: updatedMatch });
};

// This would be a POST request in a real app
export const createSeason = (leagueId, seasonName, teamIds) => {
    const newSeason = {
        id: `s${seasons.length + 1}`,
        name: seasonName,
        leagueId: leagueId,
        teams: teamIds,
    };
    seasons.push(newSeason);
    return fakeFetch({ success: true, season: newSeason });
};