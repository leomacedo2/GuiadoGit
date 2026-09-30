import { useEffect, useState } from 'react';
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { useAnalysis, profilePath } from '../analysis/AnalysisContext';
import { technologyRepositories } from '../analysis/technologyRepositories';
import { errorMessage, getClassroom, openSavedAnalysis } from '../services/api';

export default function TechnologyRepositoriesPage({ classroom = false }) {
  const { username, id, technology } = useParams();
  const [search] = useSearchParams();
  const studentId = search.get('aluno');
  const { records } = useAnalysis();
  const { state } = useLocation();
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    if (!classroom) return;
    let active = true;
    setResult(null); setError('');
    async function load() {
      const group = await getClassroom(id); // Ownership checked by the existing endpoint; database only.
      const groups = [];
      let unavailable = 0;
      const members = group.members.filter(member => !studentId || member.gitHubProfileId === studentId);
      // Sequential reads avoid a burst of requests for a large class. Never use getAnalysis/force refresh here.
      for (const member of members) {
        if (!active) return;
        if (!member.analyzedAt) continue;
        if (member.dashboard?.technologies && !member.dashboard.technologies.some(t => t.label.toLowerCase() === technology.toLowerCase())) continue;
        try {
          const snapshot = await openSavedAnalysis(member.gitHubProfileId);
          const repositories = technologyRepositories(snapshot, technology);
          if (repositories.length) groups.push({ username: member.username, name: member.name, repositories });
        } catch (failure) {
          if (failure.response?.status === 401 || failure.response?.status === 403) throw failure;
          unavailable++;
        }
      }
      if (active) setResult({ name: group.name, groups, unavailable });
    }
    load().catch(failure => { if (active) setError(errorMessage(failure)); });
    return () => { active = false; };
  }, [classroom, id, technology, studentId]);

  const stored = state?.technologyEvidence;
  const fromLink = stored?.username?.toLowerCase() === username?.toLowerCase() && stored?.technology === technology;
  const analysis = records[username?.toLowerCase()];
  const repositories = fromLink ? stored.repositories : technologyRepositories(analysis, technology);
  const missing = !classroom && !fromLink && !analysis;
  const groups = classroom ? result?.groups ?? [] : repositories.length ? [{ username, repositories }] : [];
  const count = groups.reduce((sum, group) => sum + group.repositories.length, 0);
  const back = classroom ? `/turmas/${encodeURIComponent(id)}` : profilePath(username);
  return <section>
    <Link to={back} className="btn btn-outline-secondary mb-3">Voltar</Link>
    <h1 className="h2 text-break">{technology}</h1>
    <p className="text-body-secondary">{classroom ? `Turma: ${result?.name ?? 'carregando…'}${studentId ? ' · Aluno selecionado' : ''}` : `Perfil @${username}`}</p>
    <h2 className="h5 text-break">Repositórios com evidência de {technology}</h2>
    <p className="small text-body-secondary">Evidência não significa proficiência nem uso exclusivo da tecnologia. A lista considera as evidências do snapshot, não apenas repositórios com commits no período do gráfico.</p>
    {error ? <p className="alert alert-danger" role="alert">{error}</p> : classroom && !result ? <p role="status">Carregando snapshots salvos…</p> : missing ?
      <p role="status">O snapshot deste perfil não está carregado. Abra a análise e acesse esta página pelo gráfico. Nenhuma nova análise foi iniciada.</p> : <>
      <p>{count} repositório(s) encontrado(s){classroom ? ' (contados por aluno)' : ''}.</p>
      {result?.unavailable > 0 && <p className="alert alert-warning">Não foi possível ler {result.unavailable} snapshot(s). A lista utiliza somente os dados disponíveis.</p>}
      {!count && <p role="status">Nenhum repositório com evidência desta tecnologia nos snapshots disponíveis.</p>}
      {groups.map(group => <section className="mb-4" key={group.username} aria-label={`Repositórios de @${group.username}`}>
        {classroom && <h3 className="h5 text-break">{group.name || group.username} (@{group.username})</h3>}
        <div className="row g-3">{group.repositories.map(repo => <div className="col-12 col-md-6 col-xl-4" key={repo.id}>
          <article className="card h-100 repository-card"><div className="card-body p-4">
            <h4 className="h5 text-break">{repo.name}</h4>
            {repo.description && <p className="text-body-secondary text-break">{repo.description}</p>}
            {repo.url && <a href={repo.url} target="_blank" rel="noopener noreferrer" className="text-break">Ver no GitHub</a>}
            {repo.reasons.length > 0 && <details className="mt-3 small"><summary>Ver evidências</summary><ul className="mt-2 mb-0">{repo.reasons.map(reason => <li className="text-break" key={reason}>{reason}</li>)}</ul></details>}
          </div></article>
        </div>)}</div>
      </section>)}
    </>}
  </section>;
}
