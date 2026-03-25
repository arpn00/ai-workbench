/**
 * Renders the text content of a bubble safely (no HTML injection).
 * Handles blank lines, bullet points, and numbered items gracefully.
 */
function BubbleContent({ text }) {
  return text.split('\n').map((line, idx) => {
    if (!line.trim()) {
      return <span key={idx} className="bubble-empty-line" aria-hidden="true" />;
    }

    // Lines that start with a dash/bullet become styled list items
    if (/^[-•*]\s/.test(line)) {
      return (
        <span key={idx} className="bubble-list-item">
          {line.replace(/^[-•*]\s/, '')}
        </span>
      );
    }

    return <span key={idx} className="bubble-line">{line}</span>;
  });
}

function formatTime(date) {
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

export default function ChatBubble({ message }) {
  const isUser = message.role === 'user';

  return (
    <div className={`message-row ${isUser ? 'user' : 'bot'}`}>
      <div className={`avatar ${isUser ? 'user' : 'bot'}`} aria-hidden="true">
        {isUser ? 'U' : '✈'}
      </div>

      <div className="bubble-wrapper">
        <div
          className={`bubble ${isUser ? 'user' : 'bot'}${message.isError ? ' error' : ''}`}
          role={isUser ? undefined : 'note'}
        >
          <BubbleContent text={message.content} />
        </div>
        <span className="bubble-timestamp">{formatTime(message.timestamp)}</span>
      </div>
    </div>
  );
}
