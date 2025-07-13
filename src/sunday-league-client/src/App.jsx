import './App.css';
import React, { useState } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import Sidebar from './components/Sidebar';
import Team from './components/Team';
import Match from './components/Match';
import League from './components/League';
import Leaderboard from './components/Leaderboard';

function App() {
  const [isSidebarOpen, setSidebarOpen] = useState(false);

  const toggleSidebar = () => {
    setSidebarOpen(!isSidebarOpen);
  };

  return (
    <BrowserRouter>
      <div className="app-container">
        <button className="hamburger-menu" onClick={toggleSidebar}>
          ☰
        </button>
        <Sidebar isOpen={isSidebarOpen} />
        <div className={`main-content ${isSidebarOpen ? 'shifted' : ''}`}>
          <h1>Sunday League</h1>
          <Routes>
            <Route path="/" element={<Navigate to="/team" replace />} />
            <Route path="/team" element={<Team />} />
            <Route path="/match" element={<Match />} />
            <Route path="/league" element={<League />} />
            <Route path="/leaderboard" element={<Leaderboard />} />
          </Routes>
        </div>
      </div>
    </BrowserRouter>
  );
}

export default App;
