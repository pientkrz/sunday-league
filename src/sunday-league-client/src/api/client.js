const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

export async function api(path, options = {}) {
  const headers = new Headers(options.headers);
  if (options.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  const response = await fetch(`${apiBaseUrl}${path}`, { ...options, headers, credentials: 'include' });
  if (response.status === 204) return null;
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error ?? 'Something went wrong. Please try again.');
  }
  return response.json();
}

export async function boardApi(path, method, body) {
  const csrf = await api('/api/auth/csrf');
  return api(path, { method, body: JSON.stringify(body), headers: { 'X-CSRF-TOKEN': csrf.token } });
}

export async function boardFormApi(path, formData) {
  const csrf = await api('/api/auth/csrf');
  return api(path, { method: 'POST', body: formData, headers: { 'X-CSRF-TOKEN': csrf.token } });
}
