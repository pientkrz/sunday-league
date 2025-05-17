// ...existing code...
import Team from './Team';
import Match from './Match';

function League({ teams, matches }) {
  return (
    <div style={{ border: '1px solid #ddd', padding: 16, marginBottom: 16 }}>
      <h4>Teams</h4>
      {teams.map(team => <Team key={team.id} team={team} />)}
      <h4>Matches</h4>
      {matches.map(match => <Match key={match.id} match={match} />)}
    </div>
  );
}

export default League;
// ...existing code...
