export default function SkillCard({ skill, detailed = false }) {
  return <article className="card h-100"><div className="card-body">
    <p className="small text-body-secondary mb-1">{skill.category}</p>
    <h3 className="h5">{skill.name}</h3>
    <span className="badge bg-primary-subtle text-primary-emphasis mb-2">{skill.evidenceLevel}</span>
    <p className="small text-body-secondary mb-0">Evidência em {skill.repositoryCount} repositório(s)</p>
    {detailed && <details className="mt-3"><summary>Ver evidências ({skill.repositoryCount} repositórios)</summary><ul className="mt-3 mb-0 ps-3">{skill.evidence.map((item, index) =>
      <li className="mb-2 small text-break" key={`${item.repositoryId}-${index}`}>
        <a href={item.repositoryUrl} target="_blank" rel="noopener noreferrer">{item.repositoryName}</a>: {item.reason}
      </li>)}</ul></details>}
  </div></article>;
}
