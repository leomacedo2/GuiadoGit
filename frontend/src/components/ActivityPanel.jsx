import { lazy, Suspense, useId, useState } from 'react';
import { activityWindow, monthLabel } from '../analysis/activityData';
const ActivityChart = lazy(() => import('./ActivityChart'));

export default function ActivityPanel({ activity, classroom = false }) {
  const [period, setPeriod] = useState(12);
  const id = useId();
  const { months, series } = activityWindow(activity, period);
  return <section className="card mb-4" aria-label="Commits por tecnologia"><div className="card-body">
    <div className="d-flex flex-wrap justify-content-between gap-3 mb-2">
      <h2 className="h5">Commits por tecnologia</h2>
      {activity && <div><label htmlFor={id} className="visually-hidden">Período da atividade</label><select id={id} className="form-select form-select-sm" value={period} onChange={e => setPeriod(Number(e.target.value))}>
        <option value={6}>Últimos 6 meses</option><option value={12}>Últimos 12 meses</option>
      </select></div>}
    </div>
    <p className="small text-body-secondary">Quantidade de commits {classroom ? 'dos alunos' : 'do usuário'}, por mês, em repositórios onde cada tecnologia foi detectada.</p>
    <p className="small text-body-secondary">Os commits são associados às tecnologias detectadas nos repositórios onde ocorreram. Um mesmo commit pode aparecer em mais de uma tecnologia quando o repositório usa múltiplas tecnologias. As contagens não representam proficiência.</p>
    {!activity ? <p className="mb-0">Este snapshot ainda não possui histórico de commits. Atualize a análise pelo GitHub para gerar esse gráfico.</p> : <>
      <p className="small text-body-secondary">Até cinco tecnologias com mais commits na janela de 12 meses, mantidas no filtro de 6 meses. Meses em UTC, pela data de registro do commit (committer). Os dados refletem a coleta dos snapshots, sem atualização automática.</p>
      {activity.collectedAt && <p className="small text-body-secondary">{classroom ? 'Coleta mais antiga entre os históricos disponíveis' : 'Histórico coletado em'}: {new Date(activity.collectedAt).toLocaleString('pt-BR')}.</p>}
      {activity.missingStudents > 0 && <p className="small text-warning-emphasis">{activity.missingStudents} de {activity.studentCount} aluno(s) ainda não possuem histórico de commits atualizado. O agregado utiliza somente os dados disponíveis.</p>}
      {activity.isPartial && <div className="alert alert-warning small" role="status">
        <p className="mb-1">Cobertura parcial do histórico de commits. Valores positivos são contagens mínimas observadas; — indica ausência de contagem completa, e não zero commits.</p>
        {activity.warnings?.length > 0 && <ul className="mb-0">{activity.warnings.map(warning => <li key={warning}>{warning}</li>)}</ul>}
      </div>}
      {!classroom && <p className="small text-body-secondary">Histórico completo em {activity.completedRepositories} de {activity.eligibleRepositories} repositórios elegíveis; {activity.inspectedRepositories} consultados. Forks e repositórios arquivados ficam fora deste gráfico.</p>}
      {series.length ? <>
        <Suspense fallback={<p role="status">Carregando gráfico…</p>}><ActivityChart months={months} series={series} /></Suspense>
        <details className="small mt-3"><summary>Ver atividade em tabela</summary><div className="table-responsive">
          <table className="table table-sm mt-2"><caption>Commits por tecnologia e mês. 0 indica nenhum commit na cobertura completa; — indica contagem indisponível ou incompleta.</caption>
            <thead><tr><th scope="col">Mês (UTC)</th>{series.map(item => <th scope="col" key={item.name}>{item.name}</th>)}</tr></thead>
            <tbody>{months.map((month, index) => <tr key={month}><th scope="row">{monthLabel(month)}</th>{series.map(item => <td key={item.name}>{item.counts[index] ?? '—'}</td>)}</tr>)}</tbody>
          </table></div></details>
      </> : <p className="mb-0">{activity.isPartial ? 'Não há contagens de commits disponíveis na cobertura coletada.' : 'Nenhum commit encontrado nos repositórios elegíveis desta janela.'}</p>}
    </>}
  </div></section>;
}
