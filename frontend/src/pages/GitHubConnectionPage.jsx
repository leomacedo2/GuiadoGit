import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { apiOrigin, connectGitHub, disconnectGitHub, errorMessage, getGitHubConnection } from '../services/api';

export default function GitHubConnectionPage() {
  const { user } = useAuth();
  const [connection, setConnection] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const popup = useRef(null);
  const mounted = useRef(false);

  async function refresh() {
    setBusy(true); setError('');
    try { const result = await getGitHubConnection(); if (mounted.current) setConnection(result); }
    catch (failure) { if (mounted.current) setError(errorMessage(failure)); }
    finally { if (mounted.current) setBusy(false); }
  }
  useEffect(() => {
    mounted.current = true;
    if (user) refresh();
    function completed(event) {
      if (event.origin !== apiOrigin || event.source !== popup.current || event.data?.type !== 'github-connection-complete') return;
      setNotice(typeof event.data.message === 'string' ? event.data.message : 'Atualize o estado da conexão.');
      popup.current?.close(); popup.current = null;
      refresh();
    }
    window.addEventListener('message', completed);
    return () => { mounted.current = false; window.removeEventListener('message', completed); popup.current?.close(); };
  }, [user]);

  function connect() {
    setError('');
    try {
      popup.current?.close(); popup.current = connectGitHub();
      setNotice('Conclua a autorização na janela do GitHub. Se o estado não atualizar ao voltar, use “Atualizar estado”.');
    } catch (failure) { setError(failure.message); }
  }
  async function disconnect() {
    setBusy(true); setError('');
    try {
      await disconnectGitHub();
      if (mounted.current) {
        setConnection({ enabled: true, connected: false });
        setNotice('Conexão removida da plataforma. Suas análises foram preservadas. Para revogar também a autorização no GitHub, acesse Settings → Applications → Authorized OAuth Apps.');
      }
    } catch (failure) { if (mounted.current) setError(errorMessage(failure)); }
    finally { if (mounted.current) setBusy(false); }
  }
  if (!user) return <p>Entre na sua conta da plataforma para conectar o GitHub. <Link to="/login">Entrar</Link></p>;
  return <section className="card"><div className="card-body">
    <h1 className="h3">GitHub</h1>
    {error && <div className="alert alert-danger" role="alert">{error}</div>}
    {notice && <p role="status">{notice}</p>}
    {busy && <p role="status">Atualizando conexão…</p>}
    {connection?.enabled === false && <p>A conexão GitHub ainda não foi habilitada neste ambiente.</p>}
    {connection?.connected ? <>
      <div className="d-flex align-items-center gap-3 mb-3">
        <img src={connection.avatarUrl} alt="" width="64" height="64" className="rounded-circle" />
        <div><strong>@{connection.username}</strong><p className="mb-0">GitHub conectado</p></div>
      </div>
      <p>{connection.requiresReconnect ? 'A autorização precisa ser renovada. Reconecte para voltar a utilizá-la nas análises.' : 'Análises podem usar sua autenticação GitHub.'}</p>
      {connection.rateLimit?.observedAt && <p className="small text-body-secondary">
        Requests restantes observadas: {connection.rateLimit.remaining ?? 'não informado'}
        {connection.rateLimit.resetAt && <> · Reset: {new Date(connection.rateLimit.resetAt).toLocaleString('pt-BR')}</>}
        {' · '}Observado em {new Date(connection.rateLimit.observedAt).toLocaleString('pt-BR')}. A quota pode ser compartilhada com outros aplicativos.
      </p>}
      {connection.requiresReconnect && <button className="btn btn-primary me-2" disabled={busy} onClick={connect}>Reconectar GitHub</button>}
      <button className="btn btn-outline-danger" disabled={busy} onClick={disconnect}>Desconectar</button>
    </> : connection?.enabled && <>
      <p>Conecte sua conta GitHub para autorizar análises usando sua própria autenticação GitHub.</p>
      <button className="btn btn-primary" disabled={busy} onClick={connect}>Conectar GitHub</button>
    </>}
    <button className="btn btn-outline-secondary ms-2" disabled={busy} onClick={refresh}>Atualizar estado</button>
  </div></section>;
}
