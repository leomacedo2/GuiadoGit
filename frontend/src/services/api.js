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
  if (accessToken && (config.url.startsWith('/api/me/') || config.url === '/api/auth/me')) {
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
export async function getSavedProfiles() { return (await api.get('/api/me/profiles')).data; }
export async function saveProfile(id) { await api.put(`/api/me/profiles/${id}`); }
export async function removeProfile(id) { await api.delete(`/api/me/profiles/${id}`); }
export async function openSavedAnalysis(id) { return (await api.get(`/api/me/profiles/${id}/analysis`)).data; }

export async function getPortfolio(username) {
  const { data } = await api.get(`/api/github/profile/${encodeURIComponent(username)}`);
  return data;
}

export async function getAnalysis(username, force = false) {
  const path = `/api/analyses/${encodeURIComponent(username)}`;
  const { data } = await (force ? api.post(`${path}/refresh`) : api.get(path));
  return data;
}
