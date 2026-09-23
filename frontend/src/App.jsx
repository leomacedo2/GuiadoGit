import { useState } from 'react';
import { Link, NavLink, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { AnalysisProvider, profilePath, resultPath, useAnalysis } from './analysis/AnalysisContext';
import AuthPage from './pages/AuthPage';
import AnalyzePage from './pages/AnalyzePage';
import AnalysisResultPage from './pages/AnalysisResultPage';
import SavedAnalysesPage from './pages/SavedAnalysesPage';
import GitHubConnectionPage from './pages/GitHubConnectionPage';
import ClassroomsPage from './pages/ClassroomsPage';
import ClassroomPage from './pages/ClassroomPage';
import { errorMessage } from './services/api';

export default function App() { return <AnalysisProvider><AppContent /></AnalysisProvider>; }
function RequireAccount({ children }) { return useAuth().user ? children : <Navigate to="/" replace />; }
function AppContent() {
  const { user, logout } = useAuth();
  const { activeUsername, remember, select } = useAnalysis();
  const location = useLocation();
  const navigate = useNavigate();
  const [menuOpen, setMenuOpen] = useState(false);
  const [signingOut, setSigningOut] = useState(false);
  const [logoutError, setLogoutError] = useState('');
  async function signOut() {
    setSigningOut(true); setLogoutError('');
    try { await logout(); setMenuOpen(false); navigate('/'); }
    catch (error) { setLogoutError(`Não foi possível encerrar a sessão. ${errorMessage(error)}`); }
    finally { setSigningOut(false); }
  }
  const [theme, setTheme] = useState(() => document.documentElement.getAttribute('data-bs-theme') || 'dark');
  const entrance = ['/', '/login', '/cadastro'].includes(location.pathname);
  function toggleTheme() {
    const next = theme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-bs-theme', next); setTheme(next);
    try { localStorage.setItem('portfolio-theme', next); } catch { /* Theme still works without storage. */ }
  }
  function openAnalysis(result) {
    remember(result); select(result.profile.username.toLowerCase()); navigate(profilePath(result.profile.username));
  }
  const links = [
    ...(user ? [['/minhas-analises', 'Minhas análises']] : []), ['/analisar', 'Analisar perfil'],
    ...(user ? [['/turmas', 'Turmas']] : []),
    ...(activeUsername ? [[profilePath(activeUsername), 'Perfil']] : []),
    [resultPath('/skills', activeUsername), 'Skills'], [resultPath('/recomendacoes', activeUsername), 'Recomendações'],
    [resultPath('/repositorios', activeUsername), 'Repositórios'], ...(user ? [['/github', 'Conexão GitHub']] : [])
  ];
  return <div className="app-shell">
    <header className="border-bottom bg-body">
      <div className="container py-3 d-flex flex-wrap align-items-center justify-content-between gap-2">
        <Link to="/" className="fw-bold fs-5 text-body text-decoration-none">GuiaDoGit</Link>
        <div className="d-flex flex-wrap align-items-center gap-2">
          {!entrance && (user ? <><span className="small account-name text-break">{user.nome}</span><button className="btn btn-sm btn-outline-secondary" disabled={signingOut} onClick={signOut}>Sair</button></>
            : <Link className="btn btn-sm btn-outline-primary" to="/">Entrar / Criar conta</Link>)}
          <button className="btn btn-sm btn-outline-secondary" onClick={toggleTheme} aria-label="Modo escuro" aria-pressed={theme === 'dark'}>{theme === 'dark' ? 'Tema claro' : 'Tema escuro'}</button>
        </div>
      </div>
      {!entrance && <nav className="container pb-3" aria-label="Navegação principal">
        <button className="btn btn-outline-secondary d-md-none mb-2" onClick={() => setMenuOpen(!menuOpen)} aria-expanded={menuOpen} aria-controls="main-navigation">Menu</button>
        <div id="main-navigation" className={`${menuOpen ? 'd-block' : 'd-none'} d-md-block`}>
          <div className="nav nav-pills flex-column flex-md-row gap-1">
            {links.map(([path, label]) => <NavLink key={label} to={path} onClick={() => setMenuOpen(false)} className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>{label}</NavLink>)}
          </div>
        </div>
      </nav>}
    </header>
    <main className="container py-4 py-md-5">
      {logoutError && <div className="alert alert-danger" role="alert">{logoutError}</div>}
      <Routes>
        <Route path="/" element={<AuthPage key="entrance" entrance />} />
        <Route path="/login" element={<AuthPage key="login" entrance />} />
        <Route path="/cadastro" element={<AuthPage key="register" register />} />
        <Route path="/analisar" element={<AnalyzePage onOpen={openAnalysis} />} />
        <Route path="/perfil/:username" element={<AnalysisResultPage view="profile" />} />
        <Route path="/perfil" element={<Navigate to={activeUsername ? profilePath(activeUsername) : '/analisar'} replace />} />
        <Route path="/skills" element={<AnalysisResultPage view="skills" />} />
        <Route path="/recomendacoes" element={<AnalysisResultPage view="recommendations" />} />
        <Route path="/repositorios" element={<AnalysisResultPage view="repositories" />} />
        <Route path="/minhas-analises" element={<SavedAnalysesPage key={user?.id || 'visitor'} onOpen={openAnalysis} />} />
        <Route path="/github" element={<GitHubConnectionPage key={user?.id || 'visitor'} />} />
        <Route path="/turmas" element={<RequireAccount><ClassroomsPage key={user?.id} /></RequireAccount>} />
        <Route path="/turmas/:id" element={<RequireAccount><ClassroomPage key={`${user?.id}-${location.pathname}`} onOpen={openAnalysis} /></RequireAccount>} />
        <Route path="*" element={<div className="alert alert-secondary">Página não encontrada. <Link to="/analisar">Analisar um perfil</Link></div>} />
      </Routes>
    </main>
    <footer className="container pb-4 small text-body-secondary">Evidências em repositórios públicos, não certificação de domínio técnico.</footer>
  </div>;
}
