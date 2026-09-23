import { createContext, useCallback, useContext, useRef, useState } from 'react';
import { getAnalysis } from '../services/api';

const AnalysisContext = createContext(null);
export const usernamePattern = /^[a-z\d](?:[a-z\d]|-(?=[a-z\d])){0,38}$/i;
export const profilePath = username => `/perfil/${encodeURIComponent(username.toLowerCase())}`;
export const resultPath = (path, username) => username ? `${path}?perfil=${encodeURIComponent(username)}` : path;

export function AnalysisProvider({ children }) {
  const [records, setRecords] = useState({});
  const [activeUsername, select] = useState('');
  const memory = useRef({});
  const pending = useRef(new Map());
  const remember = useCallback(result => {
    const key = result.profile.username.toLowerCase();
    const next = { ...memory.current };
    delete next[key]; next[key] = result;
    // Only public snapshots, bounded to five profiles. No tokens or persistent browser storage.
    while (Object.keys(next).length > 5) delete next[Object.keys(next)[0]];
    memory.current = next; setRecords(next);
  }, []);
  const fetchAnalysis = useCallback((username, force = false) => {
    const normalized = username.trim().toLowerCase();
    if (!usernamePattern.test(normalized)) return Promise.reject(new Error('Informe um username válido, sem @ ou URL.'));
    const key = `${normalized}:${force}`;
    if (!pending.current.has(key)) {
      const request = getAnalysis(normalized, force).then(result => { remember(result); return result; })
        .finally(() => pending.current.delete(key));
      pending.current.set(key, request);
    }
    return pending.current.get(key);
  }, [remember]);
  const ensureAnalysis = useCallback(username => {
    const key = username.toLowerCase();
    return Object.hasOwn(memory.current, key) ? Promise.resolve(memory.current[key]) : fetchAnalysis(key);
  }, [fetchAnalysis]);
  return <AnalysisContext.Provider value={{ records, activeUsername, select, remember, fetchAnalysis, ensureAnalysis }}>{children}</AnalysisContext.Provider>;
}
export const useAnalysis = () => useContext(AnalysisContext);
