import React, { useState } from 'react';

const MatchList = ({ matches, teams, onUpdateMatch }) => {
    const [editingMatchId, setEditingMatchId] = useState(null);
    const [scores, setScores] = useState({ home: '', away: '' });

    if (!matches || !teams || teams.length === 0) {
        return <p>No matches to display.</p>;
    }

    const handleEditClick = (match) => {
        setEditingMatchId(match.id);
        // Pre-fill scores, handling null for scheduled matches
        setScores({ home: match.homeScore ?? '', away: match.awayScore ?? '' });
    };

    const handleCancelClick = () => {
        setEditingMatchId(null);
        setScores({ home: '', away: '' });
    };

    const handleSaveClick = (matchId) => {
        const homeScore = parseInt(scores.home, 10);
        const awayScore = parseInt(scores.away, 10);

        // Basic validation to ensure scores are numbers
        if (!isNaN(homeScore) && !isNaN(awayScore)) {
            onUpdateMatch(matchId, { homeScore, awayScore });
            setEditingMatchId(null); // Exit editing mode
        } else {
            alert('Please enter valid numbers for scores.');
        }
    };

    const handleScoreChange = (e, type) => {
        setScores({ ...scores, [type]: e.target.value });
    };

    const teamNameMap = teams.reduce((acc, team) => {
        acc[team.id] = team.name;
        return acc;
    }, {});

    const sortedMatches = [...matches].sort((a, b) => new Date(a.date) - new Date(b.date));

    return (
        <div className="match-list-container">
            <ul className="match-list">
                {sortedMatches.map((match) => (
                    <li key={match.id} className="match-item">
                        {editingMatchId === match.id ? (
                            <>
                                <span className="match-date">{new Date(match.date).toLocaleDateString()}</span>
                                <span className="match-teams">
                                    {teamNameMap[match.homeTeamId]} vs {teamNameMap[match.awayTeamId]}
                                </span>
                                <div className="match-score-editor">
                                    <input
                                        type="number"
                                        value={scores.home}
                                        onChange={(e) => handleScoreChange(e, 'home')}
                                        className="score-input"
                                    />
                                    <span> - </span>
                                    <input
                                        type="number"
                                        value={scores.away}
                                        onChange={(e) => handleScoreChange(e, 'away')}
                                        className="score-input"
                                    />
                                    <button onClick={() => handleSaveClick(match.id)} className="button-small">Save</button>
                                    <button onClick={handleCancelClick} className="button-small-secondary">Cancel</button>
                                </div>
                            </>
                        ) : (
                            <>
                                <span className="match-date">{new Date(match.date).toLocaleDateString()}</span>
                                <span className="match-teams">
                                    {teamNameMap[match.homeTeamId] || 'Unknown Team'} vs {teamNameMap[match.awayTeamId] || 'Unknown Team'}
                                </span>
                                <span className="match-score">
                                    {match.status === 'finished' ? `${match.homeScore} - ${match.awayScore}` : 'Scheduled'}
                                </span>
                                <button onClick={() => handleEditClick(match)} className="button-small">Edit</button>
                            </>
                        )}
                    </li>
                ))}
            </ul>
        </div>
    );
};

export default MatchList;