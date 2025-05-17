import { useState } from 'react'
import reactLogo from './assets/react.svg'
import viteLogo from '/vite.svg'
import './App.css'
import Team from './components/Team';
import Match from './components/Match';
import League from './components/League';
import Leaderboard from './components/Leaderboard';

function App() {
  const [count, setCount] = useState(0)

  // Sample data
  const teams = [
    { id: 1, name: 'Red Lions', info: 'Lorem ipsum dolor sit amet, consectetur adipiscing elit.' },
    { id: 2, name: 'Blue Sharks', info: 'Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.' },
  ];
  const matches = [
    { id: 1, date: '2025-05-10', teamA: 'Red Lions', teamB: 'Blue Sharks', scoreA: 2, scoreB: 1 },
    { id: 2, date: '2025-05-17', teamA: 'Blue Sharks', teamB: 'Red Lions', scoreA: 0, scoreB: 3 },
  ];
  const league = {
    teams,
    matches,
  };
  const leaderboard = [
    { team: 'Red Lions', points: 6 },
    { team: 'Blue Sharks', points: 0 },
  ];

  return (
    <>
      <div>
        <a href="https://vite.dev" target="_blank">
          <img src={viteLogo} className="logo" alt="Vite logo" />
        </a>
        <a href="https://react.dev" target="_blank">
          <img src={reactLogo} className="logo react" alt="React logo" />
        </a>
      </div>
      <h1>Vite + React</h1>
      <div className="card">
        <button onClick={() => setCount((count) => count + 1)}>
          count is {count}
        </button>
        <p>
          Edit <code>src/App.jsx</code> and save to test HMR
        </p>
      </div>
      <p className="read-the-docs">
        Click on the Vite and React logos to learn more
      </p>
      <div style={{ padding: 24 }}>
        <h1>Sunday League</h1>
        <h2>Team</h2>
        <Team team={teams[0]} />
        <h2>Match</h2>
        <Match match={matches[0]} />
        <h2>League</h2>
        <League teams={teams} matches={matches} />
        <h2>Leaderboard</h2>
        <Leaderboard standings={leaderboard} />
      </div>
    </>
  )
}

export default App
