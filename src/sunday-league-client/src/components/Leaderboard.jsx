// ...existing code...
function Leaderboard({ standings }) {
  return (
    <table style={{ borderCollapse: 'collapse', width: '100%' }}>
      <thead>
        <tr>
          <th style={{ border: '1px solid #ccc', padding: 8 }}>Team</th>
          <th style={{ border: '1px solid #ccc', padding: 8 }}>Points</th>
        </tr>
      </thead>
      <tbody>
        {standings.map((row, idx) => (
          <tr key={row.team}>
            <td style={{ border: '1px solid #ccc', padding: 8 }}>{row.team}</td>
            <td style={{ border: '1px solid #ccc', padding: 8 }}>{row.points}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default Leaderboard;
// ...existing code...
