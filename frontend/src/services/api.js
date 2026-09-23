import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080',
  // Include the free backend's cold start without retrying mutations automatically.
  timeout: import.meta.env.PROD ? 330000 : 245000,
});

// Platform token only, kept in memory. Never a GitHub or database credential.
let accessToken = null;
let sessionVersion = 0;
let refreshInFlight = null;
let initialSession = null;
let logoutInFlight = null;
export function setAccessToken(value) { accessToken = value; sessionVersion++; }
const sessionRequest = { withCredentials: true, headers: { 'X-Session-Request': '1' } };
api.interceptors.request.use(config => {
  config.sessionVersion ??= sessionVersion;
  if (accessToken && (config.url.startsWith('/api/me/') || config.url === '/api/auth/me'
    || config.url === '/api/auth/logout'
    || config.url.startsWith('/api/analyses/') || config.url.startsWith('/api/github/') || config.url.startsWith('/api/classes'))) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});
api.interceptors.response.use(response => response, async error => {
  const config = error.config;
  if (error.response?.status !== 401 || !config?.headers?.Authorization || config.url === '/api/auth/logout' || config.skipSessionRefresh)
    return Promise.reject(error);
  // Never replay a request from an old account after login/logout changed the session.
  if (config.sessionVersion !== sessionVersion) return Promise.reject(error);
  if (!config.sessionRetried && !logoutInFlight) {
    try {
      // A concurrent request may already have renewed the access token.
      if (config.headers.Authorization === `Bearer ${accessToken}`) await refreshSession();
      if (!accessToken || config.sessionVersion !== sessionVersion) throw error;
    } catch {
      if (config.sessionVersion === sessionVersion) {
        setAccessToken(null);
        window.dispatchEvent(new Event('session-expired'));
      }
      return Promise.reject(error);
    }
    config.sessionRetried = true;
    return api.request(config); // A non-401 failure on retry must not discard a valid session.
  }
  setAccessToken(null);
  window.dispatchEvent(new Event('session-expired'));
  return Promise.reject(error);
});

export function refreshSession() {
  if (logoutInFlight) return Promise.reject(new Error('Saída em andamento.'));
  if (!refreshInFlight) {
    const version = sessionVersion;
    refreshInFlight = api.post('/api/auth/refresh', null, { ...sessionRequest, timeout: 65000 }).then(({ data }) => {
      if (version !== sessionVersion) throw new Error('Sessão alterada.');
      accessToken = data.accessToken;
      return data;
    }).finally(() => { refreshInFlight = null; });
  }
  return refreshInFlight;
}

export function restoreSession() {
  // Shared across StrictMode effects, including the /me lookup. One silent attempt per page load.
  initialSession ??= refreshSession().then(() => getCurrentUser(true)).catch(() => { setAccessToken(null); return null; });
  return initialSession;
}

export function logoutAccount() {
  if (!logoutInFlight) {
    // Let an existing rotation finish before sending its replacement cookie for revocation.
    logoutInFlight = (refreshInFlight ?? Promise.resolve()).catch(() => {}).then(() =>
      api.post('/api/auth/logout', null, sessionRequest)
    ).then(() => { setAccessToken(null); }).finally(() => { logoutInFlight = null; });
  }
  return logoutInFlight;
}

export function errorMessage(error) {
  return error.response?.data?.detail || (error.response?.status === 429
    ? 'Muitas tentativas. Aguarde um minuto antes de tentar novamente.'
    : error.response?.data?.errors ? Object.values(error.response.data.errors).flat().join(' ')
    : 'Não foi possível concluir. O servidor pode estar iniciando; aguarde um pouco e tente novamente.');
}
export async function registerAccount(form) { return (await api.post('/api/auth/register', form)).data; }
export async function loginAccount(form) { return (await api.post('/api/auth/login', form, sessionRequest)).data; }
export async function getCurrentUser(skipSessionRefresh = false) { return (await api.get('/api/auth/me', { skipSessionRefresh })).data; }
export async function getGitHubConnection() { return (await api.get('/api/github/connection')).data; }
export async function disconnectGitHub() { await api.delete('/api/github/connection'); }
export const apiOrigin = new URL(api.defaults.baseURL, window.location.origin).origin;

export function connectGitHub() {
  if (!accessToken) throw new Error('Entre na plataforma para conectar o GitHub.');
  const name = `github-connect-${crypto.randomUUID()}`;
  const popup = window.open('about:blank', name, 'popup,width=640,height=760');
  if (!popup) throw new Error('Permita pop-ups deste site para conectar o GitHub.');
  // Top-level POST establishes a first-party correlation cookie on the backend.
  // Only the existing platform bearer is submitted; GitHub tokens never reach React.
  const form = document.createElement('form');
  form.method = 'POST'; form.action = `${api.defaults.baseURL.replace(/\/$/, '')}/api/github/connect`; form.target = name;
  const field = document.createElement('input');
  field.type = 'hidden'; field.name = 'platformToken'; field.value = accessToken;
  form.append(field); document.body.append(form);
  try { form.submit(); } finally { field.value = ''; form.remove(); }
  return popup;
}
export async function getSavedProfiles() { return (await api.get('/api/me/profiles')).data; }
export async function saveProfile(id) { await api.put(`/api/me/profiles/${id}`); }
export async function removeProfile(id) { await api.delete(`/api/me/profiles/${id}`); }
export async function openSavedAnalysis(id) { return (await api.get(`/api/me/profiles/${id}/analysis`)).data; }
export async function getClassrooms() { return (await api.get('/api/classes')).data; }
export async function getClassroom(id) { return (await api.get(`/api/classes/${encodeURIComponent(id)}`)).data; }
export async function createClassroom(name, profileIds) { return (await api.post('/api/classes', { name, profileIds })).data; }
export async function renameClassroom(id, name) { await api.put(`/api/classes/${encodeURIComponent(id)}`, { name }); }
export async function deleteClassroom(id) { await api.delete(`/api/classes/${encodeURIComponent(id)}`); }
export async function addClassroomMembers(id, profileIds) { await api.post(`/api/classes/${encodeURIComponent(id)}/members`, { profileIds }); }
export async function removeClassroomMember(id, profileId) { await api.delete(`/api/classes/${encodeURIComponent(id)}/members/${encodeURIComponent(profileId)}`); }

export async function getPortfolio(username) {
  const { data } = await api.get(`/api/github/profile/${encodeURIComponent(username)}`);
  return data;
}

export async function getAnalysis(username, force = false) {
  const path = `/api/analyses/${encodeURIComponent(username)}`;
  const { data } = await (force ? api.post(`${path}/refresh`) : api.get(path));
  return data;
}
