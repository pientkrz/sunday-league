import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getTeam } from '../api/dataService';
import { teams as allTeams, seasons as allSeasons } from '../api/mockData'; // Direct import for simplicity
import MatchList from '../components/MatchList';

const TeamPage = () => {
    const { teamId } = useParams();
    const [team, setTeam] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchData = async () => {
            setLoading(true);
            const teamData = await getTeam(teamId);
            setTeam(teamData);
            setLoading(false);
        };
        fetchData();
    }, [teamId]);

    if (loading) return <p>Loading team details...</p>;
    if (!team) return <p>Team not found.</p>;

    // Group matches by season for a more organized view
    const matchesBySeason = team.matches.reduce((acc, match) => {
        (acc[match.seasonId] = acc[match.seasonId] || []).push(match);
        return acc;
    }, {});

    const seasonNameMap = allSeasons.reduce((acc, season) => {
        acc[season.id] = season.name;
        return acc;
    }, {});

    // Sort seasons chronologically (descending)
    const sortedSeasonIds = Object.keys(matchesBySeason).sort((a, b) =>
        (seasonNameMap[b] || '').localeCompare(seasonNameMap[a] || '')
    );

    return (
        <div className="page-container">
            <Link to="/">&larr; Back to Leagues</Link>
            <h2>{team.name}</h2>
            <p>View the complete match history for {team.name}, grouped by season.</p>

            {sortedSeasonIds.length > 0 ? (
                sortedSeasonIds.map(seasonId => (
                    <div key={seasonId} style={{ marginTop: '2rem' }}>
                        <h3>{seasonNameMap[seasonId] || 'Unknown Season'}</h3>
                        <MatchList matches={matchesBySeason[seasonId]} teams={allTeams} />
                    </div>
                ))
            ) : (<p>This team has no match history.</p>)}
        </div>
    );
};

export default TeamPage;