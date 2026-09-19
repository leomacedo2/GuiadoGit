import RecommendationCard from '../components/RecommendationCard';

export default function RecommendationsPage({ recommendations }) {
  return <section aria-labelledby="recommendations-title">
    <h1 id="recommendations-title" className="h2">Recomendações de estudo</h1>
    <p className="text-body-secondary">Até três próximos passos nas trilhas com mais evidências no perfil. Pouca evidência não significa falta de conhecimento.</p>
    {!recommendations.length && <p>As regras atuais não geraram recomendações. Isso não significa que não existam outros assuntos para estudar.</p>}
    <div className="row g-3">{recommendations.map(item => <div className="col-12 col-lg-6" key={item.topic}><RecommendationCard recommendation={item} /></div>)}</div>
  </section>;
}
