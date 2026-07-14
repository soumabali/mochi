# Post-MVP Phase I: Chat / LLM Integration

**Date:** 2026-07-07
**Status:** autonomous execution
**Goal:** Mochi can chat with the user via LLM API. Cat responds with speech bubbles and mood-based behavior. Uses local-first approach (user provides own API key).

## Architecture

```
User types in chat input (tray menu "Chat with Mochi" → small WPF window)
    ↓
ChatService (Core/Services/) — manages conversation history, sends to LLM
    ↓
ILLMProvider (Core/Services/) — abstraction for different providers
    ↓
OpenAICompatibleProvider — works with OpenAI, local Ollama, LM Studio, etc.
    ↓
Response → speech bubble + cat behavior reaction (meow, happy, etc.)
```

### Key Design Decisions

- **D-I1: User provides own API key** — no built-in key, privacy-first
- **D-I2: OpenAI-compatible API** — works with OpenAI, Ollama, LM Studio, Groq, etc.
- **D-I3: Local-first option** — supports Ollama (localhost:11434) for fully offline
- **D-I4: Cat personality system prompt** — Mochi has a cat personality, responds in character
- **D-I5: Context-aware** — includes time, mood, weather in system prompt
- **D-I6: Conversation history** — limited to last 10 messages to control token usage

## Tasks

| Task | Description | Files |
|------|-------------|-------|
| I-01 | ILLMProvider interface + ChatMessage model | Core/Services/ILLMProvider.cs, Core/Models/ChatMessage.cs |
| I-02 | ChatSettings model (API URL, key, model name) | Core/Models/ChatSettings.cs |
| I-03 | OpenAICompatibleProvider — HTTP client, chat completions | Core/Services/OpenAICompatibleProvider.cs |
| I-04 | ChatService — conversation management, system prompt, context | Core/Services/ChatService.cs |
| I-05 | ChatWindow WPF — small chat interface | UI/Chat/ChatWindow.xaml + .cs |
| I-06 | Tray menu "💬 Chat with Mochi" item | TrayIconController.cs |
| I-07 | Wire ChatService into DI + App.xaml.cs | Program.cs, App.xaml.cs |
| I-08 | Cat behavior reaction to chat (meow on response, mood shift) | App.xaml.cs |
| I-09 | Chat settings in SaveData (API URL, key, model) | SaveData.cs |
| I-10 | Unit tests (ChatService, mock provider) | tests-core/ChatTests.cs |
| I-11 | Compile + test + commit | — |