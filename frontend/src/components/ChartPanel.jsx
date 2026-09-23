import { lazy, Suspense } from 'react';
const EvidenceChart = lazy(() => import('./EvidenceChart'));

export default function ChartPanel({ title, rows, description, unit = 'Repositórios' }) {
  return <section className="card h-100" aria-label={title}><div className="card-body">
    <h2 className="h5">{title}</h2>
    <p className="small text-body-secondary">{description}</p>
    {rows.length ? <>
      <Suspense fallback={<p role="status">Carregando gráfico…</p>}><EvidenceChart rows={rows} title={title} unit={unit} /></Suspense>
      <details className="small mt-3"><summary>Ver dados em tabela</summary>
        <table className="table table-sm mt-2"><caption className="visually-hidden">{title}: número de {unit.toLowerCase()}</caption>
          <thead><tr><th scope="col">Evidência</th><th scope="col" className="text-end">{unit}</th></tr></thead>
          <tbody>{rows.map(row => <tr key={row.label}><th scope="row" className="text-break fw-normal">{row.label}</th><td className="text-end">{row.count}</td></tr>)}</tbody>
        </table>
      </details>
    </> : <p>Não há evidências suficientes para este gráfico.</p>}
  </div></section>;
}
