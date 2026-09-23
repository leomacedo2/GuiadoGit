import { useState } from 'react';
import RecommendationCard from '../components/RecommendationCard';

export default function RecommendationsPage({ analysis }) {
  const tracks = analysis.learningTracks || [];
  const [area, setArea] = useState('Todas');
  const areas = ['Todas', ...new Set(tracks.map(track => track.area))];
  const selected = areas.includes(area) ? area : 'Todas';
  return <section aria-labelledby="recommendations-title">
    <h1 id="recommendations-title" className="h2">Trilhas e recomendações</h1>
    <p className="text-body-secondary">Até três trilhas relacionadas às evidências do perfil. Os próximos passos são possibilidades de estudo, não uma avaliação de proficiência.</p>
    {analysis.recommendations.length > 0 && <details className="card mb-4">
      <summary className="card-header py-3 fw-semibold">Prioridades sugeridas ({analysis.recommendations.length})</summary>
      <div className="card-body"><div className="row g-3">{analysis.recommendations.map(item => <div className="col-12 col-lg-4" key={`${item.track}-${item.topic}`}><RecommendationCard recommendation={item} /></div>)}</div></div>
    </details>}
    {tracks.length > 0 ? <>
      <div className="d-flex flex-wrap gap-2 mb-4" role="group" aria-label="Filtrar trilhas por área">
        {areas.map(value => <button key={value} className={`btn btn-sm ${selected === value ? 'btn-primary' : 'btn-outline-primary'}`} aria-pressed={selected === value} onClick={() => setArea(value)}>{value}</button>)}
      </div>
      {tracks.filter(track => selected === 'Todas' || track.area === selected).map(track => <article className="card mb-4" key={track.name}>
        <div className="card-body p-3 p-md-4">
          <p className="small text-primary-emphasis mb-1">{track.area}</p><h2 className="h4">{track.name}</h2>
          {track.area === 'Full Stack' && <p className="small text-body-secondary">Combinação de sinais frontend e backend do perfil. Eles podem vir de repositórios diferentes; sua presença não comprova integração entre as partes.</p>}
          <h3 className="h6 mt-3">Já demonstrado nos repositórios</h3>
          <div className="d-flex flex-wrap gap-2 mb-4">{track.demonstratedSkills.map(name => <span className="badge bg-success-subtle text-success-emphasis text-wrap" key={name}>✓ {name}</span>)}</div>
          <h3 className="h6">Próximos passos</h3>
          {track.progressionMessage && <p className="text-body-secondary">{track.progressionMessage}</p>}
          {!track.nextSteps.length && <p>Não há sugestões adicionais com os pré-requisitos observados neste snapshot. Isso não significa domínio da trilha nem ausência de possibilidades de estudo.</p>}
          <ol className="list-group list-group-numbered">
            {track.nextSteps.map(step => <li className="list-group-item" key={step.topic}>
              <strong>{step.topic}</strong><p className="mt-2 mb-2">{step.nextStep}</p>
              <details className="small"><summary>Por que esta sugestão?</summary>
                <p className="mt-2 text-body-secondary">{step.reason}</p>
                <ul>{step.consideredSkills.map(name => {
                  const skill = analysis.skills.find(item => item.name.toLowerCase() === name.toLowerCase());
                  return <li key={name}>{name}{skill && <> — {skill.repositoryCount} repositório(s)</>}</li>;
                })}</ul>
              </details>
            </li>)}
          </ol>
        </div>
      </article>)}
    </> : <p>{analysis.recommendations.length ? 'Este snapshot preserva recomendações da versão anterior. Atualize pelo GitHub no perfil para obter as trilhas ampliadas.' : 'Ainda não há evidências suficientes para sugerir trilhas relevantes. Isso não significa ausência de conhecimento.'}</p>}
  </section>;
}
