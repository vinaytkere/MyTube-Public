# AI Integration Guide

MyTube includes optional AI-assisted features for:
- Automatic video chapter and key-moment extraction.
- Intelligent content summarization from transcripts.
- Semantic category and tag suggestions.
- Title and metadata cleaning.

---

## Zero-Key Operation (Graceful Degradation)

**No API keys are required to use MyTube.** If no AI provider is configured:
- Video downloads, streaming, conversions, and playback function completely normally.
- MyTube uses standard yt-dlp metadata (native titles, descriptions, uploaders, and tags).
- No errors or warning popups are displayed.

---

## Supported Providers

### 1. Google Gemini (Cloud, Free Tier Available)

Google provides free access tiers with generous rate limits through Google AI Studio.

1. **Obtain an API Key:**
   - Visit [Google AI Studio](https://aistudio.google.com/).
   - Sign in and click **Get API key** > **Create API key**.
2. **Configure MyTube:**
   ```bash
   echo -n "YOUR_GEMINI_API_KEY" > secrets/gemini_api_key.secret
   chmod 600 secrets/gemini_api_key.secret
   ```
3. In `.env`, set:
   ```env
   AI_PROVIDER=gemini
   ```

---

### 2. Groq Cloud (Cloud, Free Tier Available)

Groq provides extremely fast Llama-3 inference ideal for near-instantaneous transcript processing.

1. **Obtain an API Key:**
   - Visit [Groq Console](https://console.groq.com/).
   - Navigate to **API Keys** and generate a new key.
2. **Configure MyTube:**
   ```bash
   echo -n "YOUR_GROQ_API_KEY" > secrets/groq_api_key.secret
   chmod 600 secrets/groq_api_key.secret
   ```
3. In `.env`, set:
   ```env
   AI_PROVIDER=groq
   ```

---

### 3. DeepSeek (Cloud)

1. Obtain your API key from the [DeepSeek Platform](https://platform.deepseek.com/).
2. Save the key:
   ```bash
   echo -n "YOUR_DEEPSEEK_API_KEY" > secrets/deepseek_api_key.secret
   chmod 600 secrets/deepseek_api_key.secret
   ```
3. In `.env`, set:
   ```env
   AI_PROVIDER=deepseek
   ```

---

### 4. Ollama (100% Self-Hosted & Local)

For complete privacy without sending data to external APIs, you can run Ollama on your local machine or homelab server:

1. **Install and launch Ollama:**
   ```bash
   # Pull a lightweight model
   ollama run llama3.2:3b
   ```
2. **Verify Network Accessibility:**
   Ensure Ollama is listening on an interface reachable by Docker containers (`OLLAMA_HOST=0.0.0.0:11434`).
3. **Configure MyTube in `.env`:**
   ```env
   AI_PROVIDER=ollama
   OLLAMA_ENDPOINT=http://host.docker.internal:11434
   ```

---

## Dynamic Quota & Rate Limit Handling

MyTube features built-in sliding-window rate limiting for AI requests:
- Quotas are tracked per provider in the SQLite database (`AiModelQuotas`).
- If a provider's rate limit or minute/daily budget is reached, requests back off automatically without interrupting active media downloads.
- You can monitor current usage and quota status in the **System Health** dashboard in the web UI.

