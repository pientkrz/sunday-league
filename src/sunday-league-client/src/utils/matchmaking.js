export const generateSeasonSchedule = (teams, seasonId, league, options = { type: 'single' }) => {
    if (teams.length < 2) return [];

    const schedule = [];
    const baseTeamIds = teams.map(t => t.id);

    // For an odd number of teams, add a "bye" team
    if (baseTeamIds.length % 2 !== 0) {
        baseTeamIds.push(null); // Bye team
    }
    const localTeamIds = [...baseTeamIds];

    const roundsCount = baseTeamIds.length - 1;
    const matchesPerRoundCount = baseTeamIds.length / 2;
    const startDate = new Date(); // Start from today
    const daysPerMatchDay = league.matchFrequency === 'weekly' ? 7 : 14;

    for (let round = 0; round < roundsCount; round++) {
        for (let match = 0; match < matchesPerRoundCount; match++) {
            const homeTeamId = localTeamIds[match];
            const awayTeamId = localTeamIds[localTeamIds.length - 1 - match];

            if (homeTeamId && awayTeamId) { // Skip bye matches
                const matchDate = new Date(startDate);
                const daysToAdd = round * daysPerMatchDay;
                matchDate.setDate(startDate.getDate() + daysToAdd);

                schedule.push({
                    id: `gen-${seasonId}-leg1-${round}-${match}`,
                    seasonId,
                    date: matchDate.toISOString().split('T')[0], // YYYY-MM-DD
                    homeTeamId,
                    awayTeamId,
                    homeScore: null,
                    awayScore: null,
                    status: 'scheduled',
                });
            }
        }

        // Rotate teams for the next round, keeping the first team fixed
        const lastTeam = localTeamIds.pop();
        localTeamIds.splice(1, 0, lastTeam);
    }

    if (options.type === 'double') {
        const secondLegSchedule = schedule.map(match => {
            const secondLegDate = new Date(match.date);
            const daysToAdd = roundsCount * daysPerMatchDay;
            secondLegDate.setDate(secondLegDate.getDate() + daysToAdd);
            return { ...match, id: match.id.replace('-leg1-', '-leg2-'), homeTeamId: match.awayTeamId, awayTeamId: match.homeTeamId, date: secondLegDate.toISOString().split('T')[0] };
        });
        return [...schedule, ...secondLegSchedule];
    }
    return schedule;
};