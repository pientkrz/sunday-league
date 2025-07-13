import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getSeason, getLeague, saveMatches, updateMatch } from '../api/dataService';
import { calculateStandings } from '../utils/standings';
import { generateSeasonSchedule } from '../utils/matchmaking';
import StandingsTable from '../components/StandingsTable';
import MatchList from '../components/MatchList';

const SeasonPage = () => {
    const { seasonId } = useParams();
    const [season, setSeason] = useState(null);
    const [league, setLeague] = useState(null);
    const [standings, setStandings] = useState([]);
    const [loading, setLoading] = useState(true);
    const [scheduleType, setScheduleType] = useState('single'); // 'single' or 'double'
    const [searchTerm, setSearchTerm] = useState('');

    const fetchSeasonData = async () => {
        setLoading(true);
        const seasonData = await getSeason(seasonId);
        if (seasonData) {
            const leagueData = await getLeague(seasonData.leagueId);
            setSeason(seasonData);
            setLeague(leagueData);
            const calculatedStandings = calculateStandings(seasonData.teams, seasonData.matches);
            setStandings(calculatedStandings);
        }
        setLoading(false);
    };

    useEffect(() => {
        fetchSeasonData();
    }, [seasonId]);

    const handleGenerateSchedule = async () => {
        if (!season || !league) return;
        const newMatches = generateSeasonSchedule(season.teams, season.id, league, { type: scheduleType });
        await saveMatches(newMatches);
        // Refetch data to show the new schedule
        fetchSeasonData();
    };

    const handleUpdateMatch = async (matchId, newScores) => {
        await updateMatch(matchId, newScores);
        // For simplicity and to ensure data consistency across components (standings, match list),
        // we refetch all season data. In a more complex app, you might update state locally.
        fetchSeasonData();
    };

    if (loading) return <p>Loading season details...</p>;
    if (!season) return <p>Season not found.</p>;

    const teamNameMap = season.teams.reduce((acc, team) => {
        acc[team.id] = team.name.toLowerCase();
        return acc;
    }, {});

    const filteredMatches = season.matches.filter(match => {
        const homeTeamName = teamNameMap[match.homeTeamId] || '';
        const awayTeamName = teamNameMap[match.awayTeamId] || '';
        return homeTeamName.includes(searchTerm.toLowerCase()) || awayTeamName.includes(searchTerm.toLowerCase());
    });

    const hasScheduledMatches = season.matches.some(m => m.status === 'scheduled');

    return (
        <div className="page-container">
            <Link to={`/league/${season.leagueId}`}>&larr; Back to {league.name}</Link>
            <h2>{season.name}</h2>
            <div className="season-content">
                <div className="season-main">
                    <StandingsTable standings={standings} />
                    <div className="match-list-header">
                        <h3>Matches</h3>
                        <input
                            type="text"
                            placeholder="Filter by team name..."
                            className="search-input"
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                        />
                    </div>
                    <MatchList
                        matches={filteredMatches}
                        teams={season.teams}
                        onUpdateMatch={handleUpdateMatch}
                    />
                </div>
                <div className="season-sidebar">
                    <h3>Teams</h3>
                    <ul className="list-group">
                        {season.teams.map(t => (
                            <li key={t.id} className="list-group-item"><Link to={`/team/${t.id}`}>{t.name}</Link></li>
                        ))}
                    </ul>
                    {!hasScheduledMatches && season.matches.length === 0 && (
                        <div className="schedule-generator">
                            <h4>Generate Schedule</h4>
                            <fieldset>
                                <legend>Schedule Type</legend>
                                <div>
                                    <input
                                        type="radio"
                                        id="single-rr"
                                        name="scheduleType"
                                        value="single"
                                        checked={scheduleType === 'single'}
                                        onChange={(e) => setScheduleType(e.target.value)} />
                                    <label htmlFor="single-rr">Single Round-Robin</label>
                                </div>
                                <div>
                                    <input
                                        type="radio"
                                        id="double-rr"
                                        name="scheduleType"
                                        value="double"
                                        checked={scheduleType === 'double'}
                                        onChange={(e) => setScheduleType(e.target.value)} />
                                    <label htmlFor="double-rr">Double Round-Robin</label>
                                </div>
                            </fieldset>
                            <button onClick={handleGenerateSchedule} className="button">Generate</button>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default SeasonPage;