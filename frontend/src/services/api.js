import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080',
  // Include the free backend's cold start without retrying mutations automatically.
  timeout: import.meta.env.PROD ? 330000 : 245000,
});

// Platform token only, kept in memory. Never a GitHub or database credential.
let accessToken = null;
export function setAccessToken(value) { accessToken = value; }
api.interceptors.request.use(config => {
  if (accessToken && (config.url.startsWith('/api/me/') || config.url === '/api/auth/me'
    || config.url.startsWith('/api/analyses/') || config.url.startsWith('/api/github/') || config.url.startsWith('/api/classes'))) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }
  return config;
});
api.interceptors.response.use(response => response, error => {
  if (error.response?.status === 401 && error.config?.url !== '/api/auth/login') {
    accessToken = null;
    window.dispatchEvent(new Event('session-expired'));
  }
  return Promise.reject(error);
});

export function errorMessage(error) {
  return error.response?.data?.detail || (error.response?.status === 429
    ? 'Muitas tentativas. Aguarde um minuto antes de tentar novamente.'
    : error.response?.data?.errors ? Object.values(error.response.data.errors).flat().join(' ')
    : 'Não foi possível concluir. O servidor pode estar iniciando; aguarde um pouco e tente novamente.');
}
export async function registerAccount(form) { return (await api.post('/api/auth/register', form)).data; }
export async function loginAccount(form) { return (await api.post('/api/auth/login', form)).data; }
export async function getCurrentUser() { return (await api.get('/api/auth/me')).data; }
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
