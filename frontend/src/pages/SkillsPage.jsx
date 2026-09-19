import SkillCard from '../components/SkillCard';

export default function SkillsPage({ skills }) {
  return <section aria-labelledby="skills-title">
    <h1 id="skills-title" className="h2">Skills e evidências</h1>
    <p className="text-body-secondary">Contamos repositórios distintos: 1 = Pouca evidência; 2 = Em desenvolvimento; 3–4 = Boa evidência; 5 ou mais = Forte evidência. Isso mede presença de sinais, não proficiência.</p>
    {!skills.length && <p>Nenhuma evidência identificada na consulta disponível.</p>}
    {[...new Set(skills.map(s => s.category))].map(category => <section className="mb-4" key={category}>
      <h2 className="h4">{category}</h2>
      <div className="row g-3">{skills.filter(s => s.category === category).map(skill => <div className="col-12 col-lg-6" key={skill.name}><SkillCard skill={skill} detailed /></div>)}</div>
    </section>)}
  </section>;
}
