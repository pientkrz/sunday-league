import React, { useState, useEffect, useMemo } from 'react';
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

    // Performance Improvement:
    // Create a Set of team IDs that have played matches.
    // This is calculated only when the 'matches' array changes.
    // Checking for a team's existence in a Set is much faster (O(1))
    // than iterating through the entire matches array for every team (O(n*m)).
    const teamsWithMatches = useMemo(() => {
        const teamIds = new Set();
        matches.forEach(match => {
            teamIds.add(match.homeTeamId);
            teamIds.add(match.awayTeamId);
        });
        return teamIds;
    }, [matches]);

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

    if (loading) return <p>Loading dashboard...</p>;

    return (
        <div className="page-container">
            {/* Code Quality Improvement:
                Inline styles have been replaced with CSS classes for better
                maintainability, reusability, and separation of concerns.
                (Note: These classes would need to be added to your App.css file).
            */}
            <div className="home-layout">
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
                    <form onSubmit={handleCreateTeam} className="create-team-form">
                        <input
                            type="text"
                            value={newTeamName}
                            onChange={(e) => setNewTeamName(e.target.value)}
                            placeholder="New team name"
                            className="search-input"
                        />
                        <button type="submit" className="button">Create Team</button>
                    </form>
                    <h3 style={{ marginTop: '2rem' }}>All Teams</h3>
                    <ul className="list-group">
                        {teams.map(team => {
                            // Performance Improvement:
                            // Check against the pre-calculated Set.
                            const hasMatches = teamsWithMatches.has(team.id);
                            return (
                                <li key={team.id} className="list-group-item list-item-actions">
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
