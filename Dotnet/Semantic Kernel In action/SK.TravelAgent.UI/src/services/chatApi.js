/**
 * chatApi.js — Travel Agent API client
 *
 * In development, Vite proxies all /api/* requests to the .NET backend using
 * the VITE_API_BASE_URL value from .env (see vite.config.js proxy config).
 * The client therefore uses a simple relative path ("/api/chat") — no CORS
 * configuration is needed on the API side during development.
 */

const ENDPOINT = '/api/chat';

/**
 * Sends a chat message to the Travel Agent API.
 *
 * @param {string|null} sessionId  - Existing session ID, or null for a brand-new session.
 * @param {string}      message    - The user's natural-language message.
 * @returns {Promise<{ sessionId: string, reply: string }>}
 */
export async function sendMessage(sessionId, message) {
  const response = await fetch(ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId: sessionId ?? null, message }),
  });

  if (!response.ok) {
    const text = await response.text().catch(() => `HTTP ${response.status}`);
    throw new Error(text || `HTTP ${response.status}`);
  }

  return response.json(); // { sessionId: string, reply: string }
}
