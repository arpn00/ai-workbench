export default function Header() {
  return (
    <header className="header">
      <div className="header-logo" aria-hidden="true">✈️</div>

      <div className="header-info">
        <div className="header-title">Travel Agent</div>
        <div className="header-subtitle">Your AI-powered travel planning assistant</div>
      </div>

      <div className="header-status" aria-label="Service status: online">
        <span className="status-dot" />
        Online
      </div>
    </header>
  );
}
