export default function RecommendationCard({ recommendation }) {
  return <article className="card h-100"><div className="card-body">
    <p className="small text-primary-emphasis fw-semibold">{recommendation.track}</p>
    <h3 className="h5">{recommendation.topic}</h3>
    <p className="small text-body-secondary">{recommendation.reason}</p>
    <p className="mb-0"><strong>Próximo passo:</strong> {recommendation.nextStep}</p>
    <details className="mt-3 small"><summary>Evidências consideradas</summary>
      <ul className="mt-2 ps-3">{recommendation.consideredEvidence?.map(item => <li key={item.skill}>
        <strong>{item.skill}</strong>: {item.repositoryCount} repositório(s)
        <ul>{item.evidence.map((e, index) => <li className="text-break" key={`${e.repositoryId}-${index}`}><a href={e.repositoryUrl} target="_blank" rel="noopener noreferrer">{e.repositoryName}</a>: {e.reason}</li>)}</ul>
      </li>)}</ul>
    </details>
  </div></article>;
}
