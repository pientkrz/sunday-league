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

export const getAllMatches = () => fakeFetch(allMatches);

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

// This would be a POST request in a real app
export const createTeam = (teamName) => {
    const newTeam = {
        id: `t${allTeams.length + 1}`,
        name: teamName,
    };
    allTeams.push(newTeam);
    return fakeFetch({ success: true, team: newTeam });
};

// This would be a DELETE request in a real app
export const deleteTeam = (teamId) => {
    // Safeguard: Check if the team has any matches
    const hasMatches = allMatches.some(m => m.homeTeamId === teamId || m.awayTeamId === teamId);
    if (hasMatches) {
        return fakeFetch({ success: false, error: 'Cannot delete a team with match history.' });
    }

    const teamIndex = allTeams.findIndex(t => t.id === teamId);
    if (teamIndex === -1) {
        return fakeFetch({ success: false, error: 'Team not found.' });
    }

    // Remove team from the main list
    allTeams.splice(teamIndex, 1);

    // Also remove team from any seasons they might have been added to
    seasons.forEach(s => {
        s.teams = s.teams.filter(tId => tId !== teamId);
    });

    return fakeFetch({ success: true });
};

// This would be a PUT/PATCH request in a real app
export const updateTeamName = (teamId, newName) => {
    const teamIndex = allTeams.findIndex(t => t.id === teamId);
    if (teamIndex === -1) {
        return fakeFetch({ success: false, error: 'Team not found' });
    }
    const updatedTeam = { ...allTeams[teamIndex], name: newName };
    allTeams[teamIndex] = updatedTeam;
    return fakeFetch({ success: true, team: updatedTeam });
};