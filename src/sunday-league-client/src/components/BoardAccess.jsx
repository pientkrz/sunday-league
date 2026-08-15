import { useState } from 'react';
import { api } from '../api/client';

export default function BoardAccess({ onAuthenticated }) {
  const [mode, setMode] = useState('login');
  const [form, setForm] = useState({ email: '', password: '', displayName: '', invitationToken: '' });
  const [status, setStatus] = useState({ loading: false, error: '' });
  const update = event => setForm(values => ({ ...values, [event.target.name]: event.target.value }));

  const submit = async event => {
    event.preventDefault();
    setStatus({ loading: true, error: '' });
    try {
      if (mode === 'login') await api('/api/auth/login', { method: 'POST', body: JSON.stringify({ email: form.email, password: form.password }) });
      else {
        await api('/api/auth/register', { method: 'POST', body: JSON.stringify(form) });
        await api('/api/auth/login', { method: 'POST', body: JSON.stringify({ email: form.email, password: form.password }) });
      }
      await onAuthenticated();
    } catch (error) { setStatus({ loading: false, error: error.message }); return; }
    setStatus({ loading: false, error: '' });
  };

  return <section className="access-layout"><div className="access-intro"><span className="tag">BOARD PORTAL</span><h2>Manage the league safely.</h2><p>Board accounts are restricted to invited league staff. Public standings always remain available without an account.</p><ul><li>Results Editors can submit scores.</li><li>Competition Admins manage their assigned league.</li><li>Owners control membership and configuration.</li></ul></div><form className="access-card card" onSubmit={submit}><div className="access-tabs"><button type="button" onClick={() => setMode('login')} className={mode === 'login' ? 'active' : ''}>Sign in</button><button type="button" onClick={() => setMode('register')} className={mode === 'register' ? 'active' : ''}>Accept invite</button></div><h3>{mode === 'login' ? 'Board sign in' : 'Create your board account'}</h3>{mode === 'register' && <label>Invitation code<input name="invitationToken" value={form.invitationToken} onChange={update} required autoComplete="one-time-code" /></label>}{mode === 'register' && <label>Name<input name="displayName" value={form.displayName} onChange={update} required autoComplete="name" /></label>}<label>Email<input name="email" type="email" value={form.email} onChange={update} required autoComplete="email" /></label><label>Password<input name="password" type="password" value={form.password} onChange={update} required autoComplete={mode === 'login' ? 'current-password' : 'new-password'} minLength="12" /></label>{status.error && <p className="form-error" role="alert">{status.error}</p>}<button className="primary access-submit" disabled={status.loading}>{status.loading ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}</button><p className="access-note">There is no public board registration. Ask your League Owner for an invitation.</p></form></section>;
}
