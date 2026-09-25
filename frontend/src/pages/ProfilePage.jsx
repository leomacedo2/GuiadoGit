import { useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { resultPath, useAnalysis } from "../analysis/AnalysisContext";
import { categoryEvidence, languageEvidence } from "../analysis/chartData";
import { errorMessage, saveProfile } from "../services/api";
import ProfileCard from "../components/ProfileCard";
import ChartPanel from "../components/ChartPanel";
import SkillCard from "../components/SkillCard";
import ActivityPanel from "../components/ActivityPanel";
import LogoLoading from "../components/LogoLoading";

export default function ProfilePage({ analysis }) {
  const { user } = useAuth();
  const { fetchAnalysis } = useAnalysis();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const username = analysis.profile.username;
  const route = (path) => resultPath(path, username);
  async function action(refresh) {
    if (busy) return;
    if (!refresh && !user) {
      setNotice("Entre ou crie uma conta para salvar este perfil.");
      return;
    }
    setBusy(true);
    setError("");
    setNotice("");
    try {
      if (refresh) await fetchAnalysis(username, true);
      else {
        await saveProfile(analysis.id);
        setNotice("Perfil salvo em Minhas análises.");
      }
    } catch (failure) {
      setError(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section aria-label={`Perfil analisado de ${username}`}>
      <ProfileCard portfolio={analysis.profile} />
      <div className="d-flex flex-wrap gap-3 mb-3 small text-body-secondary">
        <span>{analysis.analyzedRepositories} repositórios analisados</span>
        <span>
          Última análise:{" "}
          {analysis.analyzedAt
            ? new Date(analysis.analyzedAt).toLocaleString("pt-BR")
            : "não informada"}
        </span>
        <span>Origem: {analysis.source === "cache" ? "cache" : "GitHub"}</span>
      </div>
      <div className="d-flex flex-wrap gap-2 mb-3">
        <button
          className="btn btn-primary"
          disabled={busy || (user && !analysis.id)}
          onClick={() => action(false)}
        >
          Salvar na minha lista
        </button>
        <button
          className="btn btn-outline-primary"
          disabled={busy}
          onClick={() => action(true)}
        >
          Atualizar pelo GitHub
        </button>
        <Link className="btn btn-outline-secondary" to="/analisar">
          Analisar outro perfil
        </Link>
      </div>
      {busy && (
        <LogoLoading
          overlay
          text="Atualizando perfil… Isso pode levar alguns minutos."
          size={72}
        />
      )}
      {error && (
        <div className="alert alert-danger" role="alert">
          {error}
        </div>
      )}
      {notice && (
        <p role="status">
          {notice} {!user && <Link to="/">Entrar / Criar conta</Link>}
        </p>
      )}
      {analysis.persistenceWarning && (
        <p className="alert alert-warning">{analysis.persistenceWarning}</p>
      )}
      {analysis.isPartial && (
        <div className="alert alert-warning">
          <strong>Cobertura parcial.</strong> Algumas evidências podem não ter
          sido consideradas.
          <details className="mt-2">
            <summary>Ver limites da consulta</summary>
            <ul className="mb-0 mt-2">
              {analysis.warnings.map((w) => (
                <li key={w} className="text-break">
                  {w}
                </li>
              ))}
            </ul>
          </details>
        </div>
      )}
      {(analysis.analysisVersion || 0) < 2 && (
        <p className="small text-body-secondary">
          Este snapshot usa regras anteriores de cobertura e trilhas. Uma
          atualização explícita aplica as regras novas e consome chamadas
          GitHub.
        </p>
      )}
      <details className="small text-body-secondary mb-4">
        <summary>Detalhes da coleta e cache</summary>
        <p className="mt-2 mb-1">
          {analysis.inspectedRepositories} árvores inspecionadas ·{" "}
          {analysis.completelyInspectedRepositories} inspeções sem cortes ·{" "}
          {analysis.currentGitHubRequests ?? analysis.totalGitHubRequests}{" "}
          chamadas nesta consulta · {analysis.totalGitHubRequests} chamadas na
          coleta original.
        </p>
        {analysis.expiresAt && (
          <p>
            Frescor do cache até{" "}
            {new Date(analysis.expiresAt).toLocaleString("pt-BR")}. O snapshot
            permanece armazenado após essa data.
          </p>
        )}
      </details>
      <h2 className="h4">Resumo das skills</h2>
      <p className="small text-body-secondary">
        Os níveis representam frequência de evidências encontradas nos
        repositórios, e não certificação ou domínio técnico.
      </p>
      <div className="row g-3 mb-3">
        {analysis.skills.slice(0, 6).map((skill) => (
          <div className="col-12 col-md-6 col-xl-4" key={skill.name}>
            <SkillCard skill={skill} />
          </div>
        ))}
      </div>
      {!analysis.skills.length && (
        <p>Nenhuma skill identificada na cobertura disponível.</p>
      )}
      <Link className="btn btn-outline-primary mb-4" to={route("/skills")}>
        Ver todas as skills
      </Link>
      <div className="row g-3 mb-4">
        <div className="col-12 col-lg-6">
          <ChartPanel
            title="Linguagens mais evidenciadas"
            technologySelection
            rows={languageEvidence(analysis.skills, "all")}
            description="Linguagens por número de repositórios com sinais. Um repositório pode evidenciar várias linguagens; não são porcentagens de código."
          />
        </div>
        <div className="col-12 col-lg-6">
          <ChartPanel
            title="Skills por categoria"
            rows={categoryEvidence(analysis.skills)}
            description="Repositórios distintos com evidência em cada categoria. Skills do mesmo repositório não duplicam a contagem da categoria."
          />
        </div>
      </div>
      <ActivityPanel activity={analysis.commitActivity} />
      <h2 className="h4">Trilhas sugeridas</h2>
      {analysis.learningTracks?.length ? (
        <div className="row g-3 mb-3">
          {analysis.learningTracks.map((track) => (
            <div className="col-12 col-lg-4" key={track.name}>
              <article className="card h-100">
                <div className="card-body">
                  <p className="small text-primary-emphasis mb-1">
                    {track.area}
                  </p>
                  <h3 className="h5">{track.name}</h3>
                  <p className="small text-body-secondary">
                    Sinais de {track.demonstratedSkills.slice(0, 4).join(", ")}.
                  </p>
                  {track.nextSteps.length ? (
                    <p className="mb-0">
                      Próximo passo: {track.nextSteps[0].topic}
                    </p>
                  ) : (
                    <p className="mb-0">
                      Sem próximos passos nas regras atuais.
                    </p>
                  )}
                </div>
              </article>
            </div>
          ))}
        </div>
      ) : (
        <p>
          {analysis.recommendations.length
            ? "Veja as recomendações preservadas deste snapshot."
            : "Ainda não há evidências suficientes para sugerir uma trilha."}
        </p>
      )}
      <div className="d-flex gap-2 flex-wrap">
        <Link className="btn btn-outline-primary" to={route("/recomendacoes")}>
          Ver recomendações
        </Link>
        <Link className="btn btn-outline-secondary" to={route("/repositorios")}>
          Ver repositórios
        </Link>
      </div>
    </section>
  );
}
