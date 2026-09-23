import { useEffect, useRef, useState } from 'react';
import { useAnalysis, usernamePattern } from '../analysis/AnalysisContext';
import { errorMessage } from '../services/api';

export default function AnalyzePage({ onOpen }) {
  const { fetchAnalysis } = useAnalysis();
  const [username, setUsername] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  async function submit(event) {
    event.preventDefault();
    if (busy) return;
    const value = username.trim();
    if (!usernamePattern.test(value)) { setError('Informe um username válido, com até 39 caracteres, sem @ ou URL.'); return; }
    setBusy(true); setError('');
    try { const result = await fetchAnalysis(value); if (mounted.current) onOpen(result); }
    catch (failure) { if (mounted.current) setError(errorMessage(failure)); }
    finally { if (mounted.current) setBusy(false); }
  }
  return <section className="search-panel mx-auto">
    <h1 className="h2">Analisar perfil GitHub</h1>
    <p className="text-body-secondary">Consulte repositórios públicos e descubra evidências de tecnologias e próximos passos de estudo. Não é necessário ter conta.</p>
    <form className="card" onSubmit={submit}><div className="card-body p-4">
      <label className="form-label" htmlFor="username">Username do GitHub</label>
      <div className="d-flex flex-column flex-sm-row gap-2">
        <input id="username" className="form-control form-control-lg" value={username} onChange={e => setUsername(e.target.value)} placeholder="Digite um usuário do GitHub" required maxLength={39} disabled={busy} spellCheck={false} autoComplete="off" aria-describedby="username-help" />
        <button className="btn btn-primary btn-lg" disabled={busy}>{busy ? 'Analisando…' : 'Analisar'}</button>
      </div>
      <p id="username-help" className="form-text mb-0">Use apenas o username, sem @ ou link.</p>
    </div></form>
    {busy && <p className="mt-3" role="status">Consultando perfil, arquivos e dependências… Pode levar alguns minutos, incluindo a inicialização do servidor.</p>}
    {error && <div className="alert alert-danger mt-3" role="alert">{error}</div>}
  </section>;
}
