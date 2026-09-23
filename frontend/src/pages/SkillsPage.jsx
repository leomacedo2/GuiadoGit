import SkillCard from '../components/SkillCard';
import ChartPanel from '../components/ChartPanel';
import { technologyEvidence } from '../analysis/chartData';

export default function SkillsPage({ skills }) {
  return <section aria-labelledby="skills-title">
    <h1 id="skills-title" className="h2">Visão geral das Skills</h1>
    <p className="text-body-secondary">Contamos repositórios distintos: 1 = Pouca evidência; 2 = Em desenvolvimento; 3–4 = Boa evidência; 5 ou mais = Forte evidência. Isso mede presença de sinais, não proficiência.</p>
    {!skills.length && <p>Nenhuma evidência identificada na consulta disponível.</p>}
    <div className="mb-4"><ChartPanel title="Tecnologias com mais evidências" rows={technologyEvidence(skills)} description="Até dez tecnologias por número de repositórios. Barras maiores representam mais evidências, não maior proficiência." /></div>
    {[...new Set(skills.map(s => s.category))].sort().map(category => <details className="card mb-3" key={category}>
      <summary className="card-header py-3 fw-semibold">{category} <span className="badge text-bg-secondary ms-2">{skills.filter(s => s.category === category).length} skills</span></summary>
      <div className="card-body"><div className="row g-3">{skills.filter(s => s.category === category).map(skill => <div className="col-12 col-lg-6" key={skill.name}><SkillCard skill={skill} detailed /></div>)}</div></div>
    </details>)}
  </section>;
}
