import { useState, useCallback } from 'react';
import Header from './components/Header';
import ChatWindow from './components/ChatWindow';
import ChatInput from './components/ChatInput';
import { sendMessage } from './services/chatApi';
import './App.css';

// Suggestion chips shown in the welcome bubble for quick-start queries
const SUGGESTIONS = [
  '7-day Tokyo trip with ₹1,50,000',
  '5-day Paris trip for $1,000',
  '3-day Goa trip with ₹15,000',
  'Weekend in Singapore with SGD 800',
];

const WELCOME_MESSAGE = {
  id: 'welcome',
  role: 'assistant',
  content: [
    "Hello! I'm your AI-powered Travel Planner 🌍",
    '',
    "Tell me your destination, how many days, and your budget — I'll check feasibility and craft a day-by-day itinerary for you.",
    '',
    'You can also ask follow-up questions within the same conversation and I will remember the context.',
  ].join('\n'),
  timestamp: new Date(),
  isSuggestions: true,
};

export default function App() {
  const [messages, setMessages] = useState([WELCOME_MESSAGE]);
  const [sessionId, setSessionId] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleSend = useCallback(
    async (text) => {
      // Append user message immediately
      setMessages((prev) => [
        ...prev,
        { id: `u-${Date.now()}`, role: 'user', content: text, timestamp: new Date() },
      ]);
      setIsLoading(true);

      try {
        const data = await sendMessage(sessionId, text);
        // Persist sessionId so every subsequent turn continues the same chat history
        setSessionId(data.sessionId);
        setMessages((prev) => [
          ...prev,
          {
            id: `b-${Date.now()}`,
            role: 'assistant',
            content: data.reply,
            timestamp: new Date(),
          },
        ]);
      } catch (err) {
        setMessages((prev) => [
          ...prev,
          {
            id: `e-${Date.now()}`,
            role: 'assistant',
            content:
              '⚠️ Could not reach the travel service. Make sure the API is running and try again.',
            timestamp: new Date(),
            isError: true,
          },
        ]);
      } finally {
        setIsLoading(false);
      }
    },
    [sessionId]
  );

  return (
    <div className="app">
      <Header />

      <ChatWindow
        messages={messages}
        isLoading={isLoading}
        suggestions={SUGGESTIONS}
        onSuggestion={handleSend}
      />

      <ChatInput onSend={handleSend} isLoading={isLoading} />
    </div>
  );
}
