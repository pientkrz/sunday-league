import { useEffect, useState } from 'react';
import { api, boardApi } from '../api/client';
import './MemberDirectory.css';

export default function MemberDirectory({ onSuccess, onError }) {
  const [members, setMembers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [revoking, setRevoking] = useState('');
  const load = async () => {
    setLoading(true);
    try {
      setMembers(await api('/api/board/members'));
    } catch (requestError) {
      onError(requestError.message);
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); }, []);
  const revoke = async member => {
    setRevoking(member.membershipId);
    try {
      await boardApi(`/api/board/members/${member.membershipId}`, 'DELETE', {});
      onSuccess(`${member.displayName}'s access was revoked.`);
      await load();
    } catch (requestError) {
      onError(requestError.message);
    } finally {
      setRevoking('');
    }
  };

  return <section className="settings-card card member-directory"><span className="tag">ACTIVE ACCESS</span><h2>Board members</h2><p>Revoke access when a board role is no longer needed. Owner access is protected.</p>{loading ? <p className="loading">Loading memberships…</p> : <div className="member-directory-list">{members.map(member => <div className="member-directory-row" key={member.membershipId}><div><b>{member.displayName}</b><span>{member.email}</span></div><div><b>{member.role}</b><span>{member.leagueName ?? 'All leagues'}</span></div>{member.role === 'Owner' ? <span className="owner-protected">Owner protected</span> : <button className="danger-button" disabled={revoking === member.membershipId} onClick={() => revoke(member)}>{revoking === member.membershipId ? 'Revoking…' : 'Revoke access'}</button>}</div>)}</div>}</section>;
}
