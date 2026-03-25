import { useState, useRef, useEffect, useCallback } from 'react';

// Send icon SVG (paper-plane)
function SendIcon() {
  return (
    <svg
      width="15"
      height="15"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <line x1="22" y1="2" x2="11" y2="13" />
      <polygon points="22 2 15 22 11 13 2 9 22 2" />
    </svg>
  );
}

export default function ChatInput({ onSend, isLoading }) {
  const [value, setValue] = useState('');
  const textareaRef = useRef(null);

  // Auto-resize the textarea as the user types
  useEffect(() => {
    const ta = textareaRef.current;
    if (!ta) return;
    ta.style.height = 'auto';
    ta.style.height = `${Math.min(ta.scrollHeight, 160)}px`;
  }, [value]);

  const handleSend = useCallback(() => {
    const trimmed = value.trim();
    if (!trimmed || isLoading) return;
    setValue('');
    onSend(trimmed);
  }, [value, isLoading, onSend]);

  const handleKeyDown = (e) => {
    // Enter sends; Shift+Enter inserts a newline
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const canSend = value.trim().length > 0 && !isLoading;

  return (
    <div className="input-area">
      <div className="input-wrapper">
        <textarea
          ref={textareaRef}
          className="chat-textarea"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask about your travel plans…"
          rows={1}
          disabled={isLoading}
          aria-label="Message input"
        />

        <button
          className="send-btn"
          onClick={handleSend}
          disabled={!canSend}
          aria-label="Send message"
          title="Send (Enter)"
        >
          <SendIcon />
        </button>
      </div>

      <p className="input-hint">Enter to send · Shift + Enter for a new line</p>
    </div>
  );
}
