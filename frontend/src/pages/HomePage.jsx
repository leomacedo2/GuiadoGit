import { Link } from 'react-router-dom';
import ProfileCard from '../components/ProfileCard';
import SkillCard from '../components/SkillCard';
import RecommendationCard from '../components/RecommendationCard';

export default function HomePage({ analysis }) {
  return <section aria-label="Resumo da análise">
    <ProfileCard portfolio={analysis.profile} />
    <h2 className="h4">Principais evidências</h2>
    <p className="text-body-secondary">Os níveis refletem presença nos projetos, não domínio ou certificação de conhecimento.</p>
    <div className="row g-3 mb-3">{analysis.skills.slice(0, 6).map(skill => <div className="col-12 col-md-6 col-xl-4" key={skill.name}><SkillCard skill={skill} /></div>)}</div>
    {!analysis.skills.length && <p>Nenhuma evidência identificada nas informações disponíveis.</p>}
    <Link className="btn btn-outline-primary mb-5" to="/skills">Ver todas as skills</Link>
    <h2 className="h4">Próximos estudos</h2>
    <div className="row g-3 mb-3">{analysis.recommendations.slice(0, 3).map(item => <div className="col-12 col-lg-4" key={item.topic}><RecommendationCard recommendation={item} /></div>)}</div>
    {!analysis.recommendations.length && <p>As regras atuais não geraram recomendações para esta amostra.</p>}
    <div className="d-flex gap-2 flex-wrap"><Link className="btn btn-outline-primary" to="/recomendacoes">Ver recomendações</Link><Link className="btn btn-outline-secondary" to="/repositorios">Ver repositórios</Link></div>
  </section>;
}
