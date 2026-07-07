using System;
using System.Collections.Generic;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Chat service managing conversation with LLM. Post-MVP Phase I.
    /// Maintains conversation history, builds system prompt with cat personality
    /// + context (time, mood, weather), and sends to LLM provider.
    /// </summary>
    public sealed class ChatService
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ChatService));

        private readonly ILLMProvider _provider;
        private readonly List<ChatMessage> _history = new();
        private ChatSettings _settings = ChatSettings.Default;

        /// <summary>Max messages kept in history (system + recent conversation).</summary>
        public int MaxHistory { get; set; } = 12;

        /// <summary>Current chat settings.</summary>
        public ChatSettings Settings
        {
            get => _settings;
            set => _settings = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Current conversation history (read-only).</summary>
        public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();

        /// <summary>Fired when a response is received. Passes the response text.</summary>
        public event Action<string>? ResponseReceived;

        /// <summary>Fired when a request is sent (for UI loading state).</summary>
        public event Action? RequestSent;

        public ChatService(ILLMProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Build the cat personality system prompt with context.</summary>
        public string BuildSystemPrompt(string mood, string weather, string time)
        {
            if (!string.IsNullOrEmpty(_settings.SystemPrompt))
                return _settings.SystemPrompt;

            return $""""
You are Mochi, a cute cat desktop pet companion. You live on the user's screen.

Personality:
- You are a friendly, playful cat who loves attention
- You speak in a warm, casual tone — like a cat who can talk
- You sometimes use cat sounds (meow, purr) naturally in conversation
- You care about the user's wellbeing (remind them to drink water, take breaks)
- You are curious and ask questions
- Keep responses short (1-3 sentences) — you're a cat, not a lecturer
- Use emojis occasionally but not excessively

Current context:
- Mood: {mood}
- Weather: {weather}
- Time: {time}

Respond as Mochi the cat. Be concise and charming.
"""";
        }

        /// <summary>Send a user message and get Mochi's response.</summary>
        /// <param name="userMessage">What the user said.</param>
        /// <param name="context">Context info (mood, weather, time).</param>
        /// <returns>Mochi's response text.</returns>
        public async System.Threading.Tasks.Task<string> SendAsync(string userMessage, string mood = "Content", string weather = "unknown", string time = "unknown")
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return "...";

            if (!_settings.Enabled)
                return "Chat is disabled. Enable it in settings. 🐱";

            // Build messages list: system + history + new user message
            var systemPrompt = BuildSystemPrompt(mood, weather, time);
            var messages = new List<ChatMessage> { new("system", systemPrompt) };

            // Add recent history (excluding old system prompts)
            int startIndex = Math.Max(0, _history.Count - MaxHistory);
            for (int i = startIndex; i < _history.Count; i++)
            {
                if (_history[i].Role != "system")
                    messages.Add(_history[i]);
            }

            // Add current user message
            var userMsg = new ChatMessage("user", userMessage);
            messages.Add(userMsg);
            _history.Add(userMsg);

            RequestSent?.Invoke();
            Logger.Information("Sending chat: {Message}", userMessage.Substring(0, Math.Min(80, userMessage.Length)));

            var response = await _provider.SendAsync(messages, _settings);

            // Store response in history
            var assistantMsg = new ChatMessage("assistant", response);
            _history.Add(assistantMsg);

            // Trim history if too long
            while (_history.Count > MaxHistory * 2)
                _history.RemoveAt(0);

            ResponseReceived?.Invoke(response);
            Logger.Information("Chat response received: {Response}", response.Substring(0, Math.Min(80, response.Length)));

            return response;
        }

        /// <summary>Clear conversation history.</summary>
        public void ClearHistory()
        {
            _history.Clear();
            Logger.Information("Chat history cleared");
        }

        /// <summary>Quick response without context (for simple interactions).</summary>
        public async System.Threading.Tasks.Task<string> QuickSendAsync(string message)
        {
            return await SendAsync(message);
        }
    }
}