import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { getLeagues, getAllTeams, createTeam, getAllMatches, deleteTeam } from '../api/dataService';

const HomePage = () => {
    const [leagues, setLeagues] = useState([]);
    const [teams, setTeams] = useState([]);
    const [matches, setMatches] = useState([]);
    const [newTeamName, setNewTeamName] = useState('');
    const [loading, setLoading] = useState(true);

    const fetchData = async () => {
        setLoading(true);
        const [leaguesData, teamsData, matchesData] = await Promise.all([
            getLeagues(),
            getAllTeams(),
            getAllMatches(),
        ]);
        setLeagues(leaguesData);
        setTeams(teamsData);
        setMatches(matchesData);
        setLoading(false);
    };

    useEffect(() => {
        fetchData();
    }, []);

    const handleDeleteTeam = async (teamId) => {
        if (window.confirm('Are you sure you want to permanently delete this team?')) {
            const result = await deleteTeam(teamId);
            if (result.success) {
                fetchData(); // Refresh the lists
            } else {
                alert(`Error: ${result.error}`);
            }
        }
    };

    const handleCreateTeam = async (e) => {
        e.preventDefault();
        if (!newTeamName.trim()) {
            alert('Please enter a team name.');
            return;
        }
        await createTeam(newTeamName);
        setNewTeamName('');
        fetchData(); // Refetch all data to show the new team
    };

    if (loading) return <p>Loading leagues...</p>;

    return (
        <div className="page-container">
            <div className="home-layout" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2rem' }}>
                <div className="leagues-section">
                    <h2>Leagues</h2>
                    <ul className="list-group">
                        {leagues.map(league => (
                            <li key={league.id} className="list-group-item">
                                <Link to={`/league/${league.id}`}>{league.name}</Link>
                            </li>
                        ))}
                    </ul>
                </div>
                <div className="teams-section">
                    <h2>Manage Teams</h2>
                    <form onSubmit={handleCreateTeam} style={{ display: 'flex', marginBottom: '1.5rem' }}>
                        <input
                            type="text"
                            value={newTeamName}
                            onChange={(e) => setNewTeamName(e.target.value)}
                            placeholder="New team name"
                            className="search-input"
                            style={{ flexGrow: 1 }}
                        />
                        <button type="submit" className="button" style={{ width: 'auto', marginLeft: '1rem' }}>Create Team</button>
                    </form>
                    <h3 style={{ marginTop: '2rem' }}>All Teams</h3>
                    <ul className="list-group">
                        {teams.map(team => {
                            const hasMatches = matches.some(m => m.homeTeamId === team.id || m.awayTeamId === team.id);
                            return (
                                <li key={team.id} className="list-group-item" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                    <Link to={`/team/${team.id}`}>{team.name}</Link>
                                    {!hasMatches && (
                                        <button onClick={() => handleDeleteTeam(team.id)} className="button-small button-danger">
                                            Delete
                                        </button>
                                    )}
                                </li>
                            );
                        })}
                    </ul>
                </div>
            </div>
        </div>
    );
};

export default HomePage;