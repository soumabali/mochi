using System.Text.Json.Serialization;

namespace MochiV2.Core.Models
{
    /// <summary>
    /// Chat/LLM configuration. Post-MVP Phase I.
    /// User provides their own API key (privacy-first).
    /// Works with OpenAI, Ollama, LM Studio, Groq, etc.
    /// </summary>
    public sealed class ChatSettings
    {
        /// <summary>API base URL (e.g. https://api.openai.com/v1 or http://localhost:11434/v1).</summary>
        [JsonPropertyName("chatApiUrl")]
        public string ApiUrl { get; set; } = "http://localhost:11434/v1";

        /// <summary>API key (empty for local Ollama).</summary>
        [JsonPropertyName("chatApiKey")]
        public string ApiKey { get; set; } = "";

        /// <summary>Model name (e.g. "gpt-4o-mini", "llama3.2", "qwen2.5").</summary>
        [JsonPropertyName("chatModel")]
        public string Model { get; set; } = "llama3.2";

        /// <summary>Max tokens for response.</summary>
        [JsonPropertyName("chatMaxTokens")]
        public int MaxTokens { get; set; } = 200;

        /// <summary>Temperature (0=deterministic, 1=creative).</summary>
        [JsonPropertyName("chatTemperature")]
        public double Temperature { get; set; } = 0.8;

        /// <summary>Whether chat feature is enabled.</summary>
        [JsonPropertyName("chatEnabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>System prompt override (empty = use default cat personality).</summary>
        [JsonPropertyName("chatSystemPrompt")]
        public string SystemPrompt { get; set; } = "";

        public static ChatSettings Default => new();
    }
}