using System.Collections.Generic;
using System.Threading.Tasks;
using MochiV2.Core.Models;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// Abstraction for LLM chat providers. Post-MVP Phase I.
    /// Implementations: OpenAI-compatible (works with OpenAI, Ollama, Groq, etc.).
    /// </summary>
    public interface ILLMProvider
    {
        /// <summary>Send a chat completion request and get the response text.</summary>
        /// <param name="messages">Conversation messages (system + user + assistant).</param>
        /// <param name="settings">Chat settings (model, temperature, max tokens).</param>
        /// <returns>Assistant response text.</returns>
        Task<string> SendAsync(List<ChatMessage> messages, ChatSettings settings);
    }
}