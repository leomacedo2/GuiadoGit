import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { profilePath, resultPath, useAnalysis, usernamePattern } from '../analysis/AnalysisContext';
import { errorMessage } from '../services/api';
import ProfilePage from './ProfilePage';
import SkillsPage from './SkillsPage';
import RecommendationsPage from './RecommendationsPage';
import RepositoriesPage from './RepositoriesPage';

export default function AnalysisResultPage({ view }) {
  const params = useParams();
  const [search] = useSearchParams();
  const { records, activeUsername, ensureAnalysis, select } = useAnalysis();
  const username = (params.username || search.get('perfil') || activeUsername).toLowerCase();
  const [failure, setFailure] = useState(null);
  const [attempt, retry] = useState(0);
  const analysis = Object.hasOwn(records, username) ? records[username] : undefined;
  useEffect(() => {
    let active = true;
    if (usernamePattern.test(username)) {
      select(username); setFailure(null);
      ensureAnalysis(username).catch(error => { if (active) setFailure({ username, message: errorMessage(error) }); });
    }
    return () => { active = false; };
  }, [username, ensureAnalysis, select, attempt]);
  if (!username) return <div className="card"><div className="card-body"><p>Escolha um perfil para visualizar esta página.</p><Link className="btn btn-primary" to="/analisar">Analisar perfil</Link></div></div>;
  if (!usernamePattern.test(username)) return <div className="alert alert-warning">Username inválido. <Link to="/analisar">Analisar outro perfil</Link>.</div>;
  if (failure?.username === username) return <div className="alert alert-danger" role="alert">{failure.message} <button className="btn btn-outline-danger ms-2" onClick={() => retry(value => value + 1)}>Tentar novamente</button></div>;
  if (!analysis) return <p role="status">Carregando análise de @{username}… O servidor pode estar iniciando.</p>;
  return <>
    {view !== 'profile' && <p className="small text-body-secondary">Análise de <Link to={profilePath(username)}>@{username}</Link> · {analysis.analyzedAt && new Date(analysis.analyzedAt).toLocaleString('pt-BR')}</p>}
    {view !== 'profile' && analysis.isPartial && <p className="alert alert-warning">Cobertura parcial: estes dados refletem as evidências disponíveis. <Link to={profilePath(username)}>Ver limites no perfil</Link>.</p>}
    {view === 'profile' && <ProfilePage key={username} analysis={analysis} />}
    {view === 'skills' && <SkillsPage key={username} skills={analysis.skills} />}
    {view === 'recommendations' && <RecommendationsPage analysis={analysis} />}
    {view === 'repositories' && <RepositoriesPage portfolio={analysis.profile} />}
    {view !== 'profile' && <Link className="btn btn-outline-secondary mt-4" to={resultPath('/repositorios', username)}>Repositórios deste perfil</Link>}
  </>;
}
