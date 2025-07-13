import React from 'react';
import { Link } from 'react-router-dom';

function Sidebar({ isOpen }) {
  return (
    <div className={`sidebar ${isOpen ? 'open' : ''}`}>
      <ul>
        <li>
          <Link to="/team">Team</Link>
        </li>
        <li>
          <Link to="/match">Match</Link>
        </li>
        <li>
          <Link to="/league">League</Link>
        </li>
        <li>
          <Link to="/leaderboard">Leaderboard</Link>
        </li>
      </ul>
    </div>
  );
}

export default Sidebar;
