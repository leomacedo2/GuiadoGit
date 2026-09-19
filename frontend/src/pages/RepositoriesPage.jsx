const formatDate = (value) => new Intl.DateTimeFormat('pt-BR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));

export default function RepositoriesPage({ portfolio }) {
  return <section aria-label="Repositórios públicos">          <div className="d-flex justify-content-between align-items-baseline gap-3 mb-3">
            <h1 className="h2 mb-0">Repositórios</h1>
            <span className="small text-body-secondary">{portfolio.repositories.length} encontrados</span>
          </div>
          {portfolio.repositories.length === 0 ? <div className="alert bg-body-secondary text-body border">Este perfil ainda não possui repositórios públicos.</div> :
            <div className="row g-3">{portfolio.repositories.map((repo) => (
              <div key={repo.id} className="col-12 col-md-6 col-xl-4">
                <article className="card h-100 repository-card">
                  <div className="card-body d-flex flex-column p-4">
                    <h3 className="h5 text-break">{repo.name}</h3>
                    <p className="text-body-secondary text-break">{repo.description || 'Sem descrição informada.'}</p>
                    <div className="mt-auto">
                      <span className="badge rounded-pill bg-body-secondary text-body border mb-3">{repo.language || 'Linguagem não informada'}</span>
                      <p className="small text-body-secondary mb-3">Atualizado em <time dateTime={repo.updatedAt}>{formatDate(repo.updatedAt)}</time></p>
                      <a className="small text-break" href={repo.url} target="_blank" rel="noopener noreferrer">{repo.url}</a>
                    </div>
                  </div>
                </article>
              </div>
            ))}</div>}
</section>;
}
