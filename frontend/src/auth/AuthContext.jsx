import { createContext, useContext, useEffect, useState } from "react";
import {
  getCurrentUser,
  loginAccount,
  logoutAccount,
  restoreSession,
  setAccessToken,
} from "../services/api";
import LogoLoading from "../components/LogoLoading";

const AuthContext = createContext(null);
export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [restoring, setRestoring] = useState(true);
  useEffect(() => {
    let active = true;
    const expired = () => setUser(null);
    window.addEventListener("session-expired", expired);
    restoreSession().then((account) => {
      if (active) {
        setUser(account);
        setRestoring(false);
      }
    });
    return () => {
      active = false;
      window.removeEventListener("session-expired", expired);
    };
  }, []);
  async function logout() {
    // Keep the session visible if the server could not revoke it; let the user retry.
    await logoutAccount();
    setUser(null);
  }
  async function login(form) {
    const result = await loginAccount(form);
    setAccessToken(result.accessToken);
    try {
      setUser(await getCurrentUser(true));
    } catch (error) {
      setAccessToken(null);
      setUser(null);
      throw error;
    }
  }
  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {restoring ? (
        <LogoLoading
          overlay
          text="Restaurando sessão… A primeira conexão pode demorar enquanto o servidor inicia."
          size={72}
        />
      ) : (
        children
      )}
    </AuthContext.Provider>
  );
}
export function useAuth() {
  return useContext(AuthContext);
}
