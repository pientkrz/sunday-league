// ...existing code...
function Team({ team }) {
  return (
    <div style={{ border: '1px solid #ccc', padding: 12, marginBottom: 12 }}>
      <h3>{team.name}</h3>
      <p>{team.info}</p>
    </div>
  );
}

export default Team;
// ...existing code...
