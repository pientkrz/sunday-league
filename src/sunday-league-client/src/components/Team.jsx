// ...existing code...
function Team({ team }) {
  return (
    <div style={{ border: '1px solid #ccc', padding: 12, marginBottom: 12 }}>
      {team ? (
        <>
          <h3>{team.name}</h3>
          <p>{team.info}</p>
        </>
      ) : (
        <p>No team data available.</p>
      )}
    </div>
  );
}

export default Team;
// ...existing code...
