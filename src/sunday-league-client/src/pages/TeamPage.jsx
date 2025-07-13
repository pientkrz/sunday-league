import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getTeam, updateTeamName } from '../api/dataService';
import { teams as allTeams, seasons as allSeasons } from '../api/mockData'; // Direct import for simplicity
import MatchList from '../components/MatchList';

const TeamPage = () => {
    const { teamId } = useParams();
    const [team, setTeam] = useState(null);
    const [loading, setLoading] = useState(true);
    const [isEditing, setIsEditing] = useState(false);
    const [editedName, setEditedName] = useState('');

    const fetchData = async () => {
        setLoading(true);
        const teamData = await getTeam(teamId);
        setTeam(teamData);
        if (teamData) {
            setEditedName(teamData.name);
        }
        setLoading(false);
    };

    useEffect(() => {
        fetchData();
    }, [teamId]);

    const handleSaveName = async () => {
        if (!editedName.trim()) {
            alert('Team name cannot be empty.');
            return;
        }
        const result = await updateTeamName(teamId, editedName.trim());
        if (result.success) {
            setIsEditing(false);
            // Optimistic update for faster UI feedback, then refetch to ensure consistency
            setTeam(prev => ({ ...prev, name: editedName.trim() }));
        } else {
            alert(`Error: ${result.error}`);
        }
    };

    const handleCancelEdit = () => {
        setIsEditing(false);
        setEditedName(team.name); // Reset to original name
    };

    const handleKeyDown = (e) => {
        if (e.key === 'Enter') {
            e.preventDefault(); // Prevent form submission if it's in a form
            handleSaveName();
        }
        if (e.key === 'Escape') {
            handleCancelEdit();
        }
    };

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
            <div className="team-header">
                {isEditing ? (
                    <div className="team-name-editor">
                        <input
                            type="text"
                            value={editedName}
                            onChange={(e) => setEditedName(e.target.value)}
                            onKeyDown={handleKeyDown}
                            className="search-input"
                            style={{ fontSize: '1.5rem', fontWeight: 'bold', padding: '0.5rem' }}
                            autoFocus
                        />
                        <div className="editor-buttons">
                            <button onClick={handleSaveName} className="button-small">Save</button>
                            <button onClick={handleCancelEdit} className="button-small-secondary">Cancel</button>
                        </div>
                    </div>
                ) : (
                    <h2>{team.name}</h2>
                )}
                {!isEditing && <button onClick={() => setIsEditing(true)} className="button-small">Edit Name</button>}
            </div>
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