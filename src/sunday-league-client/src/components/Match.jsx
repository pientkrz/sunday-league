// ...existing code...
function Match({ match }) {
  return (
    <div style={{ display: 'flex', gap: 16, alignItems: 'center', border: '1px solid #eee', padding: 8, marginBottom: 8 }}>
      <span>{match.date}</span>
      <span>{match.teamA} vs {match.teamB}</span>
      <span>{match.scoreA} - {match.scoreB}</span>
    </div>
  );
}

export default Match;
// ...existing code...
