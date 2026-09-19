import { createContext, useContext, useEffect, useRef, useState } from 'react';
import { getCurrentUser, loginAccount, setAccessToken } from '../services/api';

const AuthContext = createContext(null);
export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const timer = useRef(null);
  function logout() {
    clearTimeout(timer.current);
    setAccessToken(null);
    setUser(null);
  }
  useEffect(() => {
    window.addEventListener('session-expired', logout);
    return () => { window.removeEventListener('session-expired', logout); clearTimeout(timer.current); };
  }, []);
  async function login(form) {
    const result = await loginAccount(form);
    setAccessToken(result.accessToken);
    try {
      setUser(await getCurrentUser());
      clearTimeout(timer.current);
      timer.current = setTimeout(logout, result.expiresIn * 1000);
    } catch (error) { logout(); throw error; }
  }
  return <AuthContext.Provider value={{ user, login, logout }}>{children}</AuthContext.Provider>;
}
export function useAuth() { return useContext(AuthContext); }
