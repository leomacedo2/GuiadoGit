import { useState } from 'react';
import { getAnalysis, saveProfile, errorMessage } from './services/api';
import { useAuth } from './auth/AuthContext';
import AuthPage from './pages/AuthPage';
import SavedAnalysesPage from './pages/SavedAnalysesPage';
import { NavLink, Routes, Route, Link, useNavigate, useLocation } from 'react-router-dom';
import HomePage from './pages/HomePage';
import SkillsPage from './pages/SkillsPage';
import RecommendationsPage from './pages/RecommendationsPage';
import RepositoriesPage from './pages/RepositoriesPage';

const usernamePattern = /^[a-z\d](?:[a-z\d]|-(?=[a-z\d])){0,38}$/i;

export default function App() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();
  const [theme, setTheme] = useState(() => document.documentElement.getAttribute('data-bs-theme') || 'dark');
  const [username, setUsername] = useState('leomacedo2');
  const [analysis, setAnalysis] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [saving, setSaving] = useState(false);
  const analysisPage = ['/', '/perfil', '/skills', '/recomendacoes', '/repositorios'].includes(location.pathname);

  function openAnalysis(result) {
    setAnalysis(result); setUsername(result.profile.username); setError(''); setNotice(''); navigate('/');
  }
  async function save() {
    if (!user) { setNotice('Entre ou crie uma conta para salvar este perfil.'); return; }
    if (saving || !analysis?.id) return;
    setSaving(true); setError('');
    try { await saveProfile(analysis.id); setNotice('Perfil salvo em Minhas análises.'); }
    catch (failure) { setError(errorMessage(failure)); }
    finally { setSaving(false); }
  }
  async function refresh() {
    if (loading) return;
    setLoading(true); setError(''); setNotice('');
    try { openAnalysis(await getAnalysis(analysis.profile.username, true)); }
    catch (failure) { setError(errorMessage(failure)); }
    finally { setLoading(false); }
  }

  function toggleTheme() {
    const nextTheme = theme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-bs-theme', nextTheme);
    setTheme(nextTheme);
    try {
      localStorage.setItem('portfolio-theme', nextTheme);
    } catch {
      // Switching still works when the browser does not allow saving preferences.
    }
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (loading) return;
    setError('');
    setNotice('');

    const value = username.trim();
    if (!usernamePattern.test(value)) {
      setError('Informe um username válido, com até 39 caracteres, sem @ ou URL.');
      return;
    }
    setLoading(true);
    try {
      openAnalysis(await getAnalysis(value));
    } catch (failure) {
      setError(failure.response?.data?.detail || (failure.code === 'ECONNABORTED'
        ? 'A consulta demorou demais. Tente novamente.'
        : 'Não foi possível consultar a API. Confira se o backend está em execução e tente novamente.'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="app-shell">
      <header className="border-bottom bg-body">
        <div className="container py-3 d-flex flex-wrap align-items-center justify-content-between gap-3">
          <span className="fw-semibold">Portfólio GitHub</span>
          <div className="d-flex flex-wrap align-items-center gap-2">
            {user ? <><span className="small">{user.nome}</span><Link className="btn btn-sm btn-outline-primary" to="/minhas-analises">Minhas análises</Link><button className="btn btn-sm btn-outline-secondary" onClick={logout}>Sair</button></>
              : <><Link className="btn btn-sm btn-outline-primary" to="/login">Entrar</Link><Link className="btn btn-sm btn-primary" to="/cadastro">Criar conta</Link></>}
            <button type="button" className="btn btn-sm btn-outline-secondary"
              onClick={toggleTheme} aria-label="Modo escuro" aria-pressed={theme === 'dark'}
              title={`Alternar para tema ${theme === 'dark' ? 'claro' : 'escuro'}`}>
              {theme === 'dark' ? 'Tema claro' : 'Tema escuro'}
            </button>
          </div>
        </div>
      </header>
      <nav className="container pt-3" aria-label="Navegação principal">
        <div className="nav nav-pills gap-2 flex-wrap">
          {[['/', 'Perfil'], ['/skills', 'Skills'], ['/recomendacoes', 'Recomendações'], ['/repositorios', 'Repositórios']].map(([path, label]) =>
            <NavLink key={path} to={path} end className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>{label}</NavLink>)}
        </div>
      </nav>
      <main className="container py-5">
        {analysisPage && (location.pathname === '/' || location.pathname === '/perfil' || !analysis) && <section className="search-panel mx-auto mb-5" aria-labelledby="page-title">
          <p className="text-primary-emphasis fw-semibold small text-uppercase">Seus projetos, em um só lugar</p>
          <h1 id="page-title" className="display-6 fw-bold">Analise seu portfólio</h1>
          <p className="text-body-secondary mb-4">Descubra evidências nos seus projetos e próximos passos para estudar.</p>
          <form onSubmit={handleSubmit}>
            <label className="form-label fw-semibold" htmlFor="username">GitHub username</label>
            <div className="d-flex flex-column flex-sm-row gap-2">
              <input id="username" className="form-control form-control-lg" value={username}
                onChange={(event) => setUsername(event.target.value)} placeholder="leomacedo2"
                maxLength={39} required autoComplete="off" spellCheck={false} disabled={loading}
                aria-describedby="username-help" />
              <button className="btn btn-primary btn-lg text-nowrap" type="submit" disabled={loading}>
                {loading ? <><span className="spinner-border spinner-border-sm me-2" aria-hidden="true" />Consultando…</> : 'Analisar Portfólio'}
              </button>
            </div>
            <div id="username-help" className="form-text">Digite apenas o username, sem @ ou link. Não é necessário fazer login.</div>
          </form>
        </section>}
        <div aria-live="polite" aria-atomic="true">
          {loading && <p className="text-center text-body-secondary" role="status">Analisando perfil, arquivos e dependências… Isso pode levar alguns minutos. Na primeira conexão, o servidor também pode estar iniciando.</p>}
          {error && <div className="alert alert-danger" role="alert">{error}</div>}
        </div>
        {analysis && analysisPage && <>
          <div className="card mb-4"><div className="card-body">
            <p className="mb-2">{analysis.source === 'cache' ? 'Análise recuperada do cache' : 'Análise consultada no GitHub'}{analysis.analyzedAt && <> · {new Date(analysis.analyzedAt).toLocaleString('pt-BR')}</>}</p>
            <p className="small text-body-secondary">{analysis.currentGitHubRequests ?? analysis.totalGitHubRequests} chamadas GitHub nesta consulta.{analysis.expiresAt && <> Cache {new Date(analysis.expiresAt) > new Date() ? 'válido até' : 'expirado em'} {new Date(analysis.expiresAt).toLocaleString('pt-BR')}.</>}</p>
            <div className="d-flex flex-wrap gap-2">
              <button className="btn btn-outline-primary" disabled={loading || saving || (user && !analysis.id)} onClick={save}>{saving ? 'Salvando…' : 'Salvar na minha lista'}</button>
              <button className="btn btn-outline-secondary" disabled={loading} onClick={refresh}>Atualizar pelo GitHub</button>
            </div>
            {notice && <p className="mt-3 mb-0" role="status">{notice} {!user && <><Link to="/login">Entrar</Link> / <Link to="/cadastro">Criar conta</Link></>}</p>}
          </div></div>
          {analysis.persistenceWarning && <div className="alert alert-warning">{analysis.persistenceWarning}</div>}
          <p className="small text-body-secondary">Análise de @{analysis.profile.username} · {analysis.analyzedRepositories} repositórios considerados · arquivos consultados em {analysis.inspectedRepositories} · {analysis.completelyInspectedRepositories} inspeções sem cortes · {analysis.totalGitHubRequests} chamadas na coleta original.</p>
          {analysis.isPartial && <div className="alert alert-warning">
            <strong>Cobertura parcial.</strong> As recomendações consideram apenas as evidências disponíveis.
            <details className="mt-2"><summary>Ver limites da consulta</summary><ul className="mb-0 mt-2">{analysis.warnings.map(w => <li key={w}>{w}</li>)}</ul></details>
          </div>}
        </>}
        <Routes>
          <Route path="/login" element={<AuthPage key="login" />} />
          <Route path="/cadastro" element={<AuthPage key="register" register />} />
          <Route path="/minhas-analises" element={<SavedAnalysesPage onOpen={openAnalysis} />} />
          <Route path="/" element={analysis ? <HomePage analysis={analysis} /> : <p className="text-body-secondary text-center">Informe um perfil para começar.</p>} />
          <Route path="/perfil" element={analysis ? <HomePage analysis={analysis} /> : <EmptyAnalysis />} />
          <Route path="/skills" element={analysis ? <SkillsPage skills={analysis.skills} /> : <EmptyAnalysis />} />
          <Route path="/recomendacoes" element={analysis ? <RecommendationsPage recommendations={analysis.recommendations} /> : <EmptyAnalysis />} />
          <Route path="/repositorios" element={analysis ? <RepositoriesPage portfolio={analysis.profile} /> : <EmptyAnalysis />} />
          <Route path="*" element={<div className="alert alert-secondary">Página não encontrada. <Link to="/">Voltar ao perfil</Link></div>} />
        </Routes>
      </main>
      <footer className="container pb-4 text-body-secondary small">Dados públicos fornecidos pelo GitHub.</footer>
    </div>
  );
}

function EmptyAnalysis() {
  return <p className="text-body-secondary">Faça uma consulta acima para visualizar esta página. O resultado fica em memória durante a navegação; após recarregar, consulte novamente.</p>;
}
