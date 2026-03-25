export default function TypingIndicator() {
  return (
    <div className="typing-row" aria-label="Travel Agent is thinking…" role="status">
      <div className="avatar bot" aria-hidden="true">✈</div>
      <div className="typing-bubble">
        <span className="typing-dot" />
        <span className="typing-dot" />
        <span className="typing-dot" />
      </div>
    </div>
  );
}
