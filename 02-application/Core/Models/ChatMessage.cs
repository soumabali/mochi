using System.Collections.Generic;

namespace MochiV2.Core.Models
{
    /// <summary>
    /// A single chat message in a conversation.
    /// Post-MVP Phase I: Chat/LLM integration.
    /// </summary>
    public sealed class ChatMessage
    {
        /// <summary>Role: "system", "user", or "assistant".</summary>
        public string Role { get; init; } = "user";

        /// <summary>Message content.</summary>
        public string Content { get; init; } = "";

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}