import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { errorMessage, getAnalysis, getSavedProfiles, openSavedAnalysis, removeProfile } from '../services/api';

export default function SavedAnalysesPage({ onOpen }) {
  const { user } = useAuth();
  const [profiles, setProfiles] = useState([]);
  const [busy, setBusy] = useState(true);
  const [error, setError] = useState('');
  useEffect(() => {
    let active = true;
    if (user) {
      setBusy(true);
      getSavedProfiles().then(data => { if (active) setProfiles(data); })
        .catch(e => { if (active) setError(errorMessage(e)); }).finally(() => { if (active) setBusy(false); });
    }
    return () => { active = false; };
  }, [user]);
  if (!user) return <div className="alert alert-info">Entre para ver os perfis salvos na sua conta. <Link to="/login">Entrar</Link> ou <Link to="/cadastro">Criar conta</Link>.</div>;
  async function action(profile, type) {
    if (busy) return;
    setBusy(true); setError('');
    try {
      if (type === 'remove') { await removeProfile(profile.gitHubProfileId); setProfiles(items => items.filter(p => p.gitHubProfileId !== profile.gitHubProfileId)); }
      else { onOpen(await (type === 'refresh' ? getAnalysis(profile.username, true) : openSavedAnalysis(profile.gitHubProfileId))); }
    } catch (failure) { setError(errorMessage(failure)); }
    finally { setBusy(false); }
  }
  return <section>
    <h1 className="h2">Minhas análises</h1>
    <p className="text-body-secondary">Abrir usa a última análise salva, mesmo expirada. Atualizar consulta o GitHub e renova o resultado compartilhado.</p>
    {error && <div className="alert alert-danger" role="alert">{error}</div>}
    {busy && <p role="status">Carregando… Uma atualização pode levar alguns minutos.</p>}
    {!busy && !error && !profiles.length && <div className="card"><div className="card-body p-4"><p>Você ainda não possui análises salvas.</p><Link className="btn btn-primary" to="/analisar">Fazer minha primeira análise</Link></div></div>}
    <div className="row g-3">{profiles.map(profile => <div className="col-12 col-lg-6" key={profile.gitHubProfileId}>
      <article className="card h-100"><div className="card-body">
        <div className="d-flex align-items-center gap-3 mb-3">
          <img src={profile.avatarUrl} alt="" width="56" height="56" className="rounded-circle" />
          <div className="profile-details text-break"><h2 className="h5 mb-1">{profile.nome || profile.username}</h2><span className="text-body-secondary">@{profile.username}</span></div>
        </div>
        <div className="d-flex gap-2 flex-wrap mb-3">{profile.latest?.skills.map(skill => <span key={skill} className="badge text-bg-secondary">{skill}</span>)}</div>
        <p className="small mb-2">Última atualização: {profile.latest ? new Date(profile.latest.analyzedAt).toLocaleString('pt-BR') : 'indisponível'}</p>
        <p><span className={`badge ${profile.isExpired ? 'text-bg-warning' : 'text-bg-success'}`}>{profile.isExpired ? 'Expirada' : 'Recente'}</span>{profile.latest && !profile.latest.isComplete && <span className="badge text-bg-warning ms-2">Cobertura parcial</span>}</p>
        <div className="d-flex flex-wrap gap-2">
          <button className="btn btn-primary" disabled={busy} onClick={() => action(profile, 'open')}>Ver perfil</button>
          <button className="btn btn-outline-primary" disabled={busy} onClick={() => action(profile, 'refresh')}>Atualizar</button>
          <button className="btn btn-outline-danger" disabled={busy} onClick={() => action(profile, 'remove')}>Remover</button>
        </div>
      </div></article>
    </div>)}</div>
  </section>;
}
