import logoGuiadoGitIcon from '../assets/branding/logo-guiadogit-icon.png';

export default function LogoLoading({
  text = 'Carregando…',
  size = 72,
  overlay = false,
}) {
  const loadingContent = (
    <div className="logo-loading" role="status" aria-live="polite">
      <img
        src={logoGuiadoGitIcon}
        alt=""
        aria-hidden="true"
        className="logo-loading-icon"
        style={{ width: `${size}px`, height: `${size}px` }}
      />

      <p className="logo-loading-text">{text}</p>
    </div>
  );

  if (overlay) {
    return (
      <div
        className="loading-overlay"
        aria-busy="true"
      >
        <div className="loading-overlay-content">
          {loadingContent}
        </div>
      </div>
    );
  }

  return loadingContent;
}