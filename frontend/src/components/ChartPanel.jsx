import { lazy, Suspense, useState } from 'react';
import TechnologyLimitSelect from './TechnologyLimitSelect';
import { limitTechnologies } from '../analysis/technologyLimit';
const EvidenceChart = lazy(() => import('./EvidenceChart'));

export default function ChartPanel({ title, rows, description, unit = 'Repositórios', technologySelection = false }) {
  const [technologyLimit, setTechnologyLimit] = useState('5');
  const visibleRows = technologySelection ? limitTechnologies(rows, technologyLimit) : rows;
  return <section className="card h-100" aria-label={title}><div className="card-body">
    <div className="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-2">
      <h2 className="h5">{title}</h2>
      {technologySelection && <TechnologyLimitSelect value={technologyLimit} onChange={setTechnologyLimit} />}
    </div>
    <p className="small text-body-secondary">{description}</p>
    {visibleRows.length ? <>
      <Suspense fallback={<p role="status">Carregando gráfico…</p>}><EvidenceChart rows={visibleRows} title={title} unit={unit} /></Suspense>
      <details className="small mt-3"><summary>Ver dados em tabela</summary>
        <table className="table table-sm mt-2"><caption className="visually-hidden">{title}: número de {unit.toLowerCase()}</caption>
          <thead><tr><th scope="col">Evidência</th><th scope="col" className="text-end">{unit}</th></tr></thead>
          <tbody>{visibleRows.map(row => <tr key={row.label}><th scope="row" className="text-break fw-normal">{row.label}</th><td className="text-end">{row.count}</td></tr>)}</tbody>
        </table>
      </details>
    </> : <p>Não há evidências suficientes para este gráfico.</p>}
  </div></section>;
}
