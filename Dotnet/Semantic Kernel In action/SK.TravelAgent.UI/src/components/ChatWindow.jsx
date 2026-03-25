import { useEffect, useRef } from 'react';
import ChatBubble from './ChatBubble';
import TypingIndicator from './TypingIndicator';

export default function ChatWindow({ messages, isLoading, suggestions = [], onSuggestion }) {
  const bottomRef = useRef(null);

  // Scroll to the newest message whenever messages or the loading state changes
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isLoading]);

  return (
    <main className="chat-window" aria-live="polite" aria-label="Conversation">
      {messages.map((msg, idx) => (
        <div key={msg.id}>
          <ChatBubble message={msg} />
          {/* Render suggestion chips below the first (welcome) bot message */}
          {msg.isSuggestions && suggestions.length > 0 && (
            <div style={{ paddingLeft: '2.4rem', marginTop: '-.4rem' }}>
              <div className="suggestions">
                {suggestions.map((s) => (
                  <button
                    key={s}
                    className="suggestion-chip"
                    onClick={() => onSuggestion?.(s)}
                    disabled={isLoading}
                  >
                    {s}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>
      ))}

      {isLoading && <TypingIndicator />}

      {/* Invisible anchor element kept at the bottom for auto-scrolling */}
      <div ref={bottomRef} />
    </main>
  );
}
