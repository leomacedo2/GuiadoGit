export default function ProfileCard({ portfolio }) {
  return (<article className="card border-0 shadow-sm mb-4">
            <div className="card-body p-4 d-flex flex-column flex-sm-row align-items-start gap-4">
              <img className="avatar rounded-circle" src={portfolio.avatarUrl} alt={`Avatar de ${portfolio.username}`} width="104" height="104" />
              <div className="flex-grow-1 profile-details">
                <h2 className="h3 mb-1">{portfolio.name || portfolio.username}</h2>
                <a href={portfolio.profileUrl} target="_blank" rel="noopener noreferrer">@{portfolio.username}</a>
                <p className="my-3 text-body-secondary bio">{portfolio.bio || 'Este perfil ainda não possui bio.'}</p>
                <span className="badge bg-body-secondary text-body border">{portfolio.publicRepositories} repositórios públicos</span>
              </div>
            </div>
          </article>);
}
