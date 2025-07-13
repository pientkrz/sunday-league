import React, { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getLeague, getSeasonsForLeague, getAllTeams, createSeason } from '../api/dataService';

const LeaguePage = () => {
    const { leagueId } = useParams();
    const [league, setLeague] = useState(null);
    const [seasons, setSeasons] = useState([]);
    const [allTeams, setAllTeams] = useState([]);
    const [loading, setLoading] = useState(true);
    const [showCreateForm, setShowCreateForm] = useState(false);
    const [newSeasonName, setNewSeasonName] = useState('');
    const [selectedTeamIds, setSelectedTeamIds] = useState(new Set());

    const fetchLeagueData = async () => {
        setLoading(true);
        const leagueData = await getLeague(leagueId);
        const seasonsData = await getSeasonsForLeague(leagueId);
        setLeague(leagueData);
        setSeasons(seasonsData);
        setLoading(false);
    };

    useEffect(() => {
        fetchLeagueData();
        // Fetch all teams once for the creation form
        getAllTeams().then(setAllTeams);
    }, [leagueId]);

    const handleTeamSelection = (teamId) => {
        setSelectedTeamIds(prev => {
            const newSet = new Set(prev);
            if (newSet.has(teamId)) {
                newSet.delete(teamId);
            } else {
                newSet.add(teamId);
            }
            return newSet;
        });
    };

    const handleCreateSeason = async (e) => {
        e.preventDefault();
        if (!newSeasonName || selectedTeamIds.size < 2) {
            alert('Please provide a season name and select at least 2 teams.');
            return;
        }
        await createSeason(leagueId, newSeasonName, Array.from(selectedTeamIds));
        // Reset form and refresh data
        setShowCreateForm(false);
        setNewSeasonName('');
        setSelectedTeamIds(new Set());
        fetchLeagueData(); // Refetch seasons to show the new one
    };

    if (loading) return <p>Loading league details...</p>;
    if (!league) return <p>League not found.</p>;

    return (
        <div className="page-container">
            <h2>{league.name}</h2>
            <div className="league-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                <h3>Seasons</h3>
                <button className="button" style={{ width: 'auto' }} onClick={() => setShowCreateForm(!showCreateForm)}>
                    {showCreateForm ? 'Cancel' : 'Create New Season'}
                </button>
            </div>

            {showCreateForm && (
                <div className="create-season-form" style={{ background: '#fff', padding: '1.5rem', borderRadius: '.25rem', border: '1px solid var(--border-color)', marginBottom: '1.5rem' }}>
                    <form onSubmit={handleCreateSeason}>
                        <h4>New Season Details</h4>
                        <div style={{ marginBottom: '1rem' }}>
                            <label htmlFor="seasonName" style={{ display: 'block', marginBottom: '0.5rem' }}>Season Name</label>
                            <input
                                type="text"
                                id="seasonName"
                                value={newSeasonName}
                                onChange={(e) => setNewSeasonName(e.target.value)}
                                placeholder="e.g., 2025-2026 Season"
                                required
                                style={{ width: '100%', padding: '0.5rem' }}
                            />
                        </div>
                        <div style={{ marginBottom: '1rem' }}>
                            <label style={{ display: 'block', marginBottom: '0.5rem' }}>Participating Teams</label>
                            {allTeams.map(team => (
                                <div key={team.id} style={{ marginRight: '1rem', display: 'inline-block' }}>
                                    <input type="checkbox" id={`team-${team.id}`} checked={selectedTeamIds.has(team.id)} onChange={() => handleTeamSelection(team.id)} />
                                    <label htmlFor={`team-${team.id}`} style={{ marginLeft: '0.5rem' }}>{team.name}</label>
                                </div>
                            ))}
                        </div>
                        <button type="submit" className="button" style={{ width: 'auto' }}>Create Season</button>
                    </form>
                </div>
            )}

            <ul className="list-group">
                {seasons.map(season => (
                    <li key={season.id} className="list-group-item">
                        <Link to={`/season/${season.id}`}>{season.name}</Link>
                    </li>
                ))}
            </ul>
        </div>
    );
};

export default LeaguePage;