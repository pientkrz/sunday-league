import { useCallback, useEffect, useMemo, useState } from 'react';
import './App.css';
import './components/FixtureManagement.css';
import './components/ScoringRules.css';
import { api, boardApi, boardFormApi } from './api/client';
import BoardAccess from './components/BoardAccess';

const colours = ['#2f6fed', '#e85d3f', '#7057d8', '#21a179', '#d59a13', '#c43c68'];
const initials = name => name.split(' ').map(word => word[0]).join('').slice(0, 2).toUpperCase();
const formatDate = value => new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }).format(new Date(value));
const toLocalDateTime = value => new Date(new Date(value).getTime() - new Date(value).getTimezoneOffset() * 60000).toISOString().slice(0, 16);
function TeamMark({ name, index = 0, small = false }) { return <span className={`team-mark ${small ? 'small' : ''}`} style={{ background: colours[index % colours.length] }}>{initials(name)}</span>; }

export default function App() {
  const [page, setPage] = useState('dashboard');
  const [leagues, setLeagues] = useState([]);
  const [selectedSlug, setSelectedSlug] = useState('');
  const [standings, setStandings] = useState([]);
  const [fixtures, setFixtures] = useState([]);
  const [session, setSession] = useState(null);
  const [drafts, setDrafts] = useState({});
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const currentLeague = leagues.find(league => league.slug === selectedSlug);

  const loadPublic = useCallback(async () => {
    try {
      setLoading(true);
      const list = await api('/api/public/leagues');
      setLeagues(list);
      setSelectedSlug(current => current || list[0]?.slug || '');
      setError('');
    } catch (requestError) { setError(requestError.message); }
    finally { setLoading(false); }
  }, []);
  const loadLeague = useCallback(async () => {
    if (!selectedSlug) return;
    try {
      const [table, schedule] = await Promise.all([api(`/api/public/leagues/${selectedSlug}/standings`), api(`/api/public/leagues/${selectedSlug}/fixtures`)]);
      setStandings(table); setFixtures(schedule); setDrafts({}); setError('');
    } catch (requestError) { setError(requestError.message); }
  }, [selectedSlug]);
  const loadSession = useCallback(async () => { try { setSession(await api('/api/auth/session')); } catch { setSession(null); } }, []);
  useEffect(() => { loadPublic(); loadSession(); }, [loadPublic, loadSession]);
  useEffect(() => { loadLeague(); }, [loadLeague]);

  const statusFor = index => !currentLeague ? '' : index < currentLeague.promotionPlaces ? 'promoted' : index >= standings.length - currentLeague.relegationPlaces ? 'relegated' : index < currentLeague.promotionPlaces + currentLeague.playoffPlaces ? 'playoff' : '';
  const canEdit = useMemo(() => session?.memberships.some(item => ['Owner', 'CompetitionAdmin', 'ResultsEditor'].includes(item.role) && (!item.leagueId || item.leagueId === currentLeague?.id)), [session, currentLeague]);
  const canConfigure = useMemo(() => session?.memberships.some(item => ['Owner', 'CompetitionAdmin'].includes(item.role) && (!item.leagueId || item.leagueId === currentLeague?.id)), [session, currentLeague]);
  const saveResult = async fixture => {
    const draft = drafts[fixture.id] ?? { homeScore: fixture.homeScore ?? '', awayScore: fixture.awayScore ?? '' };
    if (draft.homeScore === '' || draft.awayScore === '') { setError('Enter both scores before saving.'); return; }
    try {
      await boardApi(`/api/board/matches/${fixture.id}/result`, 'PUT', { homeScore: Number(draft.homeScore), awayScore: Number(draft.awayScore), version: fixture.version });
      setNotice('Result saved. Standings have been recalculated.');
      await loadLeague();
    } catch (requestError) { setError(requestError.message); }
  };
  const updateFixture = async (fixture, kickoff) => {
    try {
      await boardApi(`/api/board/matches/${fixture.id}/kickoff`, 'PUT', { kickoff: new Date(kickoff).toISOString(), version: fixture.version });
      setNotice('Fixture kick-off updated.');
      await loadLeague();
    } catch (requestError) { setError(requestError.message); }
  };
  const cancelFixture = async fixture => {
    try {
      await boardApi(`/api/board/matches/${fixture.id}/cancel`, 'POST', { version: fixture.version });
      setNotice('Fixture cancelled. The schedule and audit history were retained.');
      await loadLeague();
    } catch (requestError) { setError(requestError.message); }
  };
  const logout = async () => { try { await boardApi('/api/auth/logout', 'POST', {}); } catch { /* Session will be cleared on reload if unavailable. */ } setSession(null); setPage('dashboard'); };

  return <div className="shell"><aside className="sidebar"><div className="brand"><span className="brand-ball">◔</span><span>Sunday<br /><b>League</b></span></div><nav className="main-nav"><button className={page === 'dashboard' ? 'active' : ''} onClick={() => setPage('dashboard')}><i>▦</i>Dashboard</button><button className={page === 'fixtures' ? 'active' : ''} onClick={() => setPage('fixtures')}><i>◫</i>Fixtures & results</button><button className={page === 'board' ? 'active' : ''} onClick={() => setPage('board')}><i>⚙</i>Board portal</button></nav><div className="sidebar-bottom"><div className="season-tag">SEASON 2026 / 27</div><span>{session ? `Signed in as ${session.displayName}` : 'Public league view'}</span></div></aside><main className="content"><header><div><p className="eyebrow">SUNDAY LEAGUE / 2026—27</p><h1>{page === 'dashboard' ? 'League overview' : page === 'fixtures' ? 'Fixtures & results' : 'Board portal'}</h1></div>{session ? <button className="profile" onClick={logout} title="Sign out">{initials(session.displayName)}</button> : <button className="secondary header-access" onClick={() => setPage('board')}>Board sign in</button>}</header>{notice && <div className="notice">✓ {notice}</div>}{error && <div className="form-error page-error" role="alert">{error}</div>}
    {(page === 'dashboard' || page === 'fixtures') && <div className="league-tabs">{leagues.map(league => <button key={league.id} onClick={() => setSelectedSlug(league.slug)} className={selectedSlug === league.slug ? 'selected' : ''}>{league.name}</button>)}</div>}
    {loading && <p className="loading">Loading public league data…</p>}
    {!loading && page === 'dashboard' && currentLeague && <Dashboard league={currentLeague} standings={standings} fixtures={fixtures} statusFor={statusFor} session={session} goFixtures={() => setPage('fixtures')} />}
    {!loading && page === 'fixtures' && <ManagedFixtures league={currentLeague} fixtures={fixtures} drafts={drafts} setDrafts={setDrafts} canEdit={canEdit} canConfigure={canConfigure} saveResult={saveResult} updateFixture={updateFixture} cancelFixture={cancelFixture} session={session} goBoard={() => setPage('board')} />}
    {page === 'board' && !session && <BoardAccess onAuthenticated={async () => { await loadSession(); setNotice('Signed in successfully.'); }} />}
    {page === 'board' && session && <BoardHome session={session} leagues={leagues} onSuccess={setNotice} onError={setError} />}
  </main></div>;
}

function Dashboard({ league, standings, fixtures, statusFor, session, goFixtures }) {
  const completed = fixtures.filter(fixture => fixture.status === 'Confirmed');
  return <><section className="hero"><div><span className="tag">PUBLIC STANDINGS</span><h2>{league.name}</h2><p>Live standings are published for everyone to view.</p></div><div className="hero-actions"><button className="secondary" onClick={goFixtures}>View fixtures</button>{session && <button className="primary" onClick={goFixtures}>Enter results →</button>}</div></section><section className="dashboard-grid"><div className="card standings-card"><div className="card-title"><div><span className="tag">TABLE</span><h3>Standings</h3></div><button className="link-button" onClick={goFixtures}>All fixtures →</button></div><table><thead><tr><th>#</th><th>Team</th><th>P</th><th>W</th><th>D</th><th>L</th><th>GD</th><th>Pts</th></tr></thead><tbody>{standings.map((team, index) => <tr key={team.teamId} className={statusFor(index)}><td><span className="rank">{team.position}</span></td><td className="club"><TeamMark name={team.teamName} index={index} small />{team.teamName}</td><td>{team.played}</td><td>{team.won}</td><td>{team.drawn}</td><td>{team.lost}</td><td>{team.goalDifference > 0 ? '+' : ''}{team.goalDifference}</td><td><b>{team.points}</b></td></tr>)}</tbody></table><div className="legend"><span><i className="green" />Promotion</span><span><i className="yellow" />Play-offs</span><span><i className="orange" />Relegation</span></div></div><div className="side-column"><div className="card next-card"><span className="tag">NEXT UP</span><h3>Fixtures</h3>{fixtures.filter(fixture => fixture.homeScore === null).slice(0, 2).map((fixture, index) => <div className="mini-match" key={fixture.id}><div><TeamMark name={fixture.homeTeam} index={index} small /> {fixture.homeTeam}</div><b>vs</b><div><TeamMark name={fixture.awayTeam} index={index + 1} small /> {fixture.awayTeam}</div><small>{formatDate(fixture.kickoff)}</small></div>)}<button className="wide secondary" onClick={goFixtures}>View all fixtures</button></div><div className="card stat-card"><span className="tag">SEASON PULSE</span><div><strong>{completed.length}</strong><span>Matches played</span></div><div><strong>{completed.reduce((sum, fixture) => sum + (fixture.homeScore ?? 0) + (fixture.awayScore ?? 0), 0)}</strong><span>Goals scored</span></div></div></div></section></>;
}

function Fixtures({ league, fixtures, drafts, setDrafts, canEdit, saveResult, session, goBoard }) {
  return <section className="fixtures-page"><div className="toolbar"><div><span className="tag">PUBLIC SCHEDULE</span><h2>{league?.name}</h2></div>{!session && <button className="secondary" onClick={goBoard}>Board sign in</button>}</div>{[...new Set(fixtures.map(fixture => fixture.roundNumber))].map(round => <div className="round card" key={round}><div className="round-head"><h3>Round {round}</h3><span>{fixtures.some(fixture => fixture.roundNumber === round && fixture.homeScore === null) ? 'Upcoming' : 'Completed'}</span></div>{fixtures.filter(fixture => fixture.roundNumber === round).map((fixture, index) => { const draft = drafts[fixture.id] ?? { homeScore: fixture.homeScore ?? '', awayScore: fixture.awayScore ?? '' }; return <div className="fixture" key={fixture.id}><span className="fixture-date">{formatDate(fixture.kickoff)}</span><div className="fixture-team home">{fixture.homeTeam}<TeamMark name={fixture.homeTeam} index={index} small /></div><div className="score-input"><input aria-label={`${fixture.homeTeam} score`} value={draft.homeScore} disabled={!canEdit} onChange={event => setDrafts(values => ({ ...values, [fixture.id]: { ...draft, homeScore: event.target.value } }))} placeholder="–" /><b>:</b><input aria-label={`${fixture.awayTeam} score`} value={draft.awayScore} disabled={!canEdit} onChange={event => setDrafts(values => ({ ...values, [fixture.id]: { ...draft, awayScore: event.target.value } }))} placeholder="–" />{canEdit && <button className="save-score" onClick={() => saveResult(fixture)}>Save</button>}</div><div className="fixture-team"><TeamMark name={fixture.awayTeam} index={index + 1} small />{fixture.awayTeam}</div></div>; })}</div>)}</section>;
}

function ManagedFixtures({ canConfigure, updateFixture, cancelFixture, ...props }) {
  return <><Fixtures {...props} />{canConfigure && <FixtureManagement fixtures={props.fixtures} updateFixture={updateFixture} cancelFixture={cancelFixture} />}</>;
}

function FixtureManagement({ fixtures, updateFixture, cancelFixture }) {
  const [kickoffs, setKickoffs] = useState({});
  useEffect(() => setKickoffs(Object.fromEntries(fixtures.map(fixture => [fixture.id, toLocalDateTime(fixture.kickoff)]))), [fixtures]);
  const editableFixtures = fixtures.filter(fixture => ['Scheduled', 'Postponed'].includes(fixture.status));
  if (!editableFixtures.length) return null;
  return <section className="fixture-management card"><div><span className="tag">LEAGUE ADMINISTRATION</span><h3>Manage upcoming fixtures</h3><p>Reschedule or cancel unplayed fixtures. Cancellations stay visible in the schedule.</p></div>{editableFixtures.map(fixture => <form className="fixture-admin-row" key={fixture.id} onSubmit={event => { event.preventDefault(); updateFixture(fixture, kickoffs[fixture.id]); }}><b>R{fixture.roundNumber}</b><span>{fixture.homeTeam} vs {fixture.awayTeam}</span><input aria-label={`Kick-off for ${fixture.homeTeam} versus ${fixture.awayTeam}`} type="datetime-local" value={kickoffs[fixture.id] ?? ''} onChange={event => setKickoffs(values => ({ ...values, [fixture.id]: event.target.value }))} required /><button className="secondary">Save time</button><button type="button" className="danger-button" onClick={() => cancelFixture(fixture)}>Cancel fixture</button></form>)}</section>;
}

function BoardHome({ session, leagues, onSuccess, onError }) {
  const isOwner = session.memberships.some(membership => membership.role === 'Owner');
  const canConfigure = isOwner || session.memberships.some(membership => membership.role === 'CompetitionAdmin');
  const [section, setSection] = useState('account');
  const [settings, setSettings] = useState([]);
  const [loading, setLoading] = useState(false);
  const loadSettings = async () => { setLoading(true); try { setSettings(await api('/api/board/leagues')); } catch (requestError) { onError(requestError.message); } finally { setLoading(false); } };
  useEffect(() => { if (section !== 'account') loadSettings(); }, [section]);
  return <section className="board-panel"><div className="board-tabs"><button className={section === 'account' ? 'active' : ''} onClick={() => setSection('account')}>My access</button>{canConfigure && <button className={section === 'leagues' ? 'active' : ''} onClick={() => setSection('leagues')}>League settings</button>}{isOwner && <button className={section === 'hierarchy' ? 'active' : ''} onClick={() => setSection('hierarchy')}>Hierarchy</button>}{isOwner && <button className={section === 'members' ? 'active' : ''} onClick={() => setSection('members')}>Board members</button>}</div>{section === 'account' && <div className="card settings-card"><span className="tag">BOARD ACCOUNT</span><h2>Welcome, {session.displayName}</h2><p>Your permissions are enforced by the server for every action.</p><div className="membership-list">{session.memberships.map(membership => <div key={`${membership.role}-${membership.leagueId ?? 'all'}`} className="membership"><b>{membership.role}</b><span>{membership.leagueName ?? 'All leagues'}</span></div>)}</div></div>}{loading && <p className="loading">Loading league settings…</p>}{section === 'leagues' && !loading && <LeagueSettings leagues={settings} isOwner={isOwner} onSuccess={onSuccess} onError={onError} reload={loadSettings} />}{section === 'hierarchy' && !loading && <HierarchySettings leagues={settings} onSuccess={onSuccess} onError={onError} reload={loadSettings} />}{section === 'members' && <InviteMember leagues={leagues} onSuccess={onSuccess} onError={onError} />}</section>;
}

function LeagueSettings({ leagues, isOwner, onSuccess, onError, reload }) {
  const [newLeague, setNewLeague] = useState('');
  const save = async league => { try { await boardApi(`/api/board/leagues/${league.id}`, 'PUT', { name: league.name, promotionPlaces: Number(league.promotionPlaces), relegationPlaces: Number(league.relegationPlaces), playoffPlaces: Number(league.playoffPlaces), winPoints: Number(league.winPoints), drawPoints: Number(league.drawPoints), lossPoints: Number(league.lossPoints), tiebreaker: league.tiebreaker, isPublished: league.isPublished }); onSuccess(`${league.name} saved.`); await reload(); } catch (requestError) { onError(requestError.message); } };
  const create = async event => { event.preventDefault(); try { await boardApi('/api/board/leagues', 'POST', { name: newLeague, promotionPlaces: 0, relegationPlaces: 0, playoffPlaces: 0, winPoints: 3, drawPoints: 1, lossPoints: 0, tiebreaker: 0, isPublished: false }); setNewLeague(''); onSuccess('League created.'); await reload(); } catch (requestError) { onError(requestError.message); } };
  const moveTeam = async (team, leagueId) => { try { await boardApi(`/api/board/teams/${team.id}`, 'PUT', { name: team.name, shortName: team.shortName, leagueId }); onSuccess(`${team.name} moved.`); await reload(); } catch (requestError) { onError(requestError.message); } };
  return <div className="settings-stack"><ScoringRules leagues={leagues} onSave={save} />{leagues.map(league => <LeagueCard key={league.id} league={league} allLeagues={leagues} isOwner={isOwner} onMove={moveTeam} onSave={save} onSuccess={onSuccess} onError={onError} reload={reload} />)}{isOwner && <form className="card add-league" onSubmit={create}><label>New league name<input value={newLeague} onChange={event => setNewLeague(event.target.value)} required maxLength="120" /></label><button className="primary">+ Add league</button></form>}</div>;
}

function ScoringRules({ leagues, onSave }) {
  const [drafts, setDrafts] = useState({});
  useEffect(() => setDrafts(Object.fromEntries(leagues.map(league => [league.id, { ...league }]))), [leagues]);
  const update = (leagueId, field, value) => setDrafts(values => ({ ...values, [leagueId]: { ...values[leagueId], [field]: value } }));
  return <section className="scoring-rules card"><span className="tag">STANDINGS RULES</span><h2>Points and tiebreakers</h2><p>Changes apply immediately to confirmed results. Wins must be worth at least draws, and draws at least losses.</p>{leagues.map(league => { const draft = drafts[league.id] ?? league; return <div className="scoring-rule-row" key={league.id}><b>{league.name}</b><label>Win<input type="number" min="0" max="10" value={draft.winPoints} onChange={event => update(league.id, 'winPoints', event.target.value)} /></label><label>Draw<input type="number" min="0" max="10" value={draft.drawPoints} onChange={event => update(league.id, 'drawPoints', event.target.value)} /></label><label>Loss<input type="number" min="0" max="10" value={draft.lossPoints} onChange={event => update(league.id, 'lossPoints', event.target.value)} /></label><label>Tiebreaker<select value={draft.tiebreaker} onChange={event => update(league.id, 'tiebreaker', Number(event.target.value))}><option value={0}>Goal difference, then goals for</option><option value={1}>Goals for, then goal difference</option></select></label><button className="secondary" onClick={() => onSave(draft)}>Save scoring</button></div>; })}</section>;
}

function LeagueCard({ league, allLeagues, isOwner, onMove, onSave, onSuccess, onError, reload }) {
  const [draft, setDraft] = useState(league);
  const [team, setTeam] = useState({ name: '', shortName: '' });
  useEffect(() => setDraft(league), [league]);
  const addTeam = async event => { event.preventDefault(); try { await boardApi(`/api/board/leagues/${league.id}/teams`, 'POST', team); setTeam({ name: '', shortName: '' }); onSuccess(`${team.name} added.`); await reload(); } catch (requestError) { onError(requestError.message); } };
  const createRandomRound = async () => { try { await boardApi(`/api/board/leagues/${league.id}/rounds/random`, 'POST', { kickoff: new Date(Date.now() + 7 * 86400000).toISOString() }); onSuccess(`Random round created for ${league.name}.`); } catch (requestError) { onError(requestError.message); } };
  const uploadCrest = async (teamItem, file) => { if (!file) return; const form = new FormData(); form.append('crest', file); try { await boardFormApi(`/api/board/teams/${teamItem.id}/crest`, form); onSuccess(`${teamItem.name} crest uploaded.`); await reload(); } catch (requestError) { onError(requestError.message); } };
  const dropTeam = event => { event.preventDefault(); const team = JSON.parse(event.dataTransfer.getData('application/json')); if (team.leagueId !== league.id) onMove(team, league.id); };
  return <article className="settings-card card league-card" onDragOver={event => event.preventDefault()} onDrop={dropTeam}><div className="league-card-head"><div><span className="tag">TIER {league.tier}</span><input className="league-name-input" value={draft.name} onChange={event => setDraft(values => ({ ...values, name: event.target.value }))} /></div><label className="publish-toggle"><input type="checkbox" checked={draft.isPublished} onChange={event => setDraft(values => ({ ...values, isPublished: event.target.checked }))} /> Public</label></div><div className="rule-inputs"><label>Promotion<input type="number" min="0" value={draft.promotionPlaces} onChange={event => setDraft(values => ({ ...values, promotionPlaces: event.target.value }))} /></label><label>Play-offs<input type="number" min="0" value={draft.playoffPlaces} onChange={event => setDraft(values => ({ ...values, playoffPlaces: event.target.value }))} /></label><label>Relegation<input type="number" min="0" value={draft.relegationPlaces} onChange={event => setDraft(values => ({ ...values, relegationPlaces: event.target.value }))} /></label><button className="secondary" onClick={() => onSave(draft)}>Save rules</button></div><div className="team-settings"><div><h3>Teams <small>{league.teams.length}</small></h3>{league.teams.map(teamItem => <div className="setting-team" draggable key={teamItem.id} onDragStart={event => event.dataTransfer.setData('application/json', JSON.stringify({ ...teamItem, leagueId: league.id }))}><TeamMark name={teamItem.name} small />{teamItem.name}<span>{teamItem.shortName}</span><select aria-label={`Move ${teamItem.name}`} value={league.id} onChange={event => onMove(teamItem, event.target.value)}>{allLeagues.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select><label className="crest-upload">Crest<input type="file" accept="image/png" onChange={event => uploadCrest(teamItem, event.target.files[0])} /></label></div>)}</div><form className="add-team" onSubmit={addTeam}><input placeholder="Team name" value={team.name} onChange={event => setTeam(values => ({ ...values, name: event.target.value }))} required maxLength="120" /><input placeholder="Short" value={team.shortName} onChange={event => setTeam(values => ({ ...values, shortName: event.target.value }))} required maxLength="5" /><button className="secondary">Add team</button></form></div><div className="scheduler-actions"><button className="primary" onClick={createRandomRound}>Create random round</button><span>Each team is paired once; an odd team count gets one bye.</span></div><ManualRoundBuilder league={league} onSuccess={onSuccess} onError={onError} /></article>;
}

function ManualRoundBuilder({ league, onSuccess, onError }) {
  const pairCount = Math.floor(league.teams.length / 2);
  const [kickoff, setKickoff] = useState('');
  const [pairs, setPairs] = useState(() => Array.from({ length: pairCount }, () => ({ homeTeamId: '', awayTeamId: '' })));
  useEffect(() => setPairs(Array.from({ length: pairCount }, () => ({ homeTeamId: '', awayTeamId: '' }))), [league.id, pairCount]);
  const updatePair = (index, field, value) => setPairs(values => values.map((pair, pairIndex) => pairIndex === index ? { ...pair, [field]: value } : pair));
  const submit = async event => {
    event.preventDefault();
    if (!kickoff) { onError('Choose a kick-off date and time.'); return; }
    try {
      await boardApi(`/api/board/leagues/${league.id}/rounds/manual`, 'POST', { kickoff: new Date(kickoff).toISOString(), fixtures: pairs });
      onSuccess(`Manual round created for ${league.name}.`);
      setKickoff(''); setPairs(Array.from({ length: pairCount }, () => ({ homeTeamId: '', awayTeamId: '' })));
    } catch (requestError) { onError(requestError.message); }
  };
  if (pairCount === 0) return null;
  return <form className="manual-round" onSubmit={submit}><div><span className="tag">MANUAL SCHEDULER</span><h3>Create a full round</h3><p>Choose every match once. {league.teams.length % 2 ? 'One club will have a bye.' : 'Every club must be selected.'}</p></div><label>Kick-off<input type="datetime-local" value={kickoff} onChange={event => setKickoff(event.target.value)} required /></label>{pairs.map((pair, index) => <div className="manual-fixture" key={index}><select value={pair.homeTeamId} onChange={event => updatePair(index, 'homeTeamId', event.target.value)} required><option value="">Home team</option>{league.teams.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}</select><span>vs</span><select value={pair.awayTeamId} onChange={event => updatePair(index, 'awayTeamId', event.target.value)} required><option value="">Away team</option>{league.teams.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}</select></div>)}<button className="secondary">Create manual round</button></form>;
}

function HierarchySettings({ leagues, onSuccess, onError, reload }) {
  const [order, setOrder] = useState(leagues);
  useEffect(() => setOrder(leagues), [leagues]);
  const move = (index, offset) => setOrder(items => { const next = index + offset; if (next < 0 || next >= items.length) return items; const copy = [...items]; [copy[index], copy[next]] = [copy[next], copy[index]]; return copy; });
  const save = async () => { try { await boardApi('/api/board/leagues/hierarchy', 'PUT', { leagueIds: order.map(league => league.id) }); onSuccess('League hierarchy saved.'); await reload(); } catch (requestError) { onError(requestError.message); } };
  return <div className="settings-card card"><span className="tag">PROMOTION & RELEGATION</span><h2>League hierarchy</h2><p>Order divisions from the highest tier down. Promotion and relegation markers use this sequence.</p><div className="hierarchy">{order.map((league, index) => <div className="hierarchy-row" key={league.id}><span className="tier-number">{index + 1}</span><b>{league.name}</b><span className="rule">↑ {league.promotionPlaces} promoted &nbsp; ↓ {league.relegationPlaces} relegated</span><button onClick={() => move(index, -1)} disabled={!index}>↑</button><button onClick={() => move(index, 1)} disabled={index === order.length - 1}>↓</button></div>)}</div><button className="primary hierarchy-save" onClick={save}>Save hierarchy</button></div>;
}

function InviteMember({ leagues, onSuccess, onError }) {
  const [form, setForm] = useState({ email: '', role: 'ResultsEditor', leagueId: '' });
  const [sending, setSending] = useState(false);
  const send = async event => { event.preventDefault(); setSending(true); try { await boardApi('/api/board/invitations', 'POST', { ...form, leagueId: form.leagueId || null }); setForm({ email: '', role: 'ResultsEditor', leagueId: '' }); onSuccess('Invitation created. Send the registration link through your approved email service.'); } catch (requestError) { onError(requestError.message); } finally { setSending(false); } };
  return <form className="settings-card card invite-card" onSubmit={send}><span className="tag">MEMBERSHIP</span><h2>Invite board member</h2><p>Only invited people can create an administrative account.</p><label>Email<input type="email" value={form.email} onChange={event => setForm(values => ({ ...values, email: event.target.value }))} required /></label><label>Role<select value={form.role} onChange={event => setForm(values => ({ ...values, role: event.target.value }))}><option value="CompetitionAdmin">Competition Admin</option><option value="ResultsEditor">Results Editor</option><option value="Viewer">Viewer</option></select></label><label>League scope<select value={form.leagueId} required={form.role !== 'Viewer'} onChange={event => setForm(values => ({ ...values, leagueId: event.target.value }))}><option value="">{form.role === 'Viewer' ? 'All leagues' : 'Choose a league'}</option>{leagues.map(league => <option key={league.id} value={league.id}>{league.name}</option>)}</select></label><button className="primary" disabled={sending}>{sending ? 'Creating…' : 'Create invitation'}</button></form>;
}
