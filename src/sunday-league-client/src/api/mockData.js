export const leagues = [
    { id: '1', name: 'Sunday Morning League', matchFrequency: 'weekly' },
    { id: '2', name: 'Corporate Champions League', matchFrequency: 'bi-weekly' },
];

export const teams = [
    { id: 't1', name: 'The Bald Eagles' },
    { id: 't2', name: 'Dynamo Dads' },
    { id: 't3', name: 'Real Social-distance' },
    { id: 't4', name: 'FC Inter-net' },
    { id: 't5', name: 'Aston Vanilla' },
    { id: 't6', name: 'Athletico Pathetico' },
];

export let seasons = [
    {
        id: 's1',
        name: '2023-2024 Season',
        leagueId: '1',
        teams: ['t1', 't2', 't3', 't4'],
    },
    {
        id: 's2',
        name: '2024-2025 Season',
        leagueId: '1',
        teams: ['t1', 't2', 't5', 't6'],
    },
];

export let matches = [
    // Season 1 Matches
    {
        id: 'm1',
        seasonId: 's1',
        date: '2023-09-10',
        homeTeamId: 't1',
        awayTeamId: 't2',
        homeScore: 2,
        awayScore: 2,
        status: 'finished',
    },
    {
        id: 'm2',
        seasonId: 's1',
        date: '2023-09-10',
        homeTeamId: 't3',
        awayTeamId: 't4',
        homeScore: 1,
        awayScore: 0,
        status: 'finished',
    },
    {
        id: 'm3',
        seasonId: 's1',
        date: '2023-09-17',
        homeTeamId: 't1',
        awayTeamId: 't3',
        homeScore: 3,
        awayScore: 1,
        status: 'finished',
    },
    {
        id: 'm4',
        seasonId: 's1',
        date: '2023-09-17',
        homeTeamId: 't2',
        awayTeamId: 't4',
        homeScore: 0,
        awayScore: 1,
        status: 'finished',
    },
    {
        id: 'm5',
        seasonId: 's1',
        date: '2023-09-24',
        homeTeamId: 't4',
        awayTeamId: 't1',
        homeScore: 0,
        awayScore: 2,
        status: 'finished',
    },
    {
        id: 'm6',
        seasonId: 's1',
        date: '2023-09-24',
        homeTeamId: 't2',
        awayTeamId: 't3',
        homeScore: null,
        awayScore: null,
        status: 'scheduled',
    },
    // Season 2 Matches (initially empty)
];