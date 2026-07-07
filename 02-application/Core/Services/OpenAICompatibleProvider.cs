using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MochiV2.Core.Models;
using Serilog;

namespace MochiV2.Core.Services
{
    /// <summary>
    /// OpenAI-compatible chat provider. Post-MVP Phase I.
    /// Works with any API that follows OpenAI's /v1/chat/completions format:
    /// OpenAI, Ollama, LM Studio, Groq, Together AI, etc.
    /// </summary>
    public sealed class OpenAICompatibleProvider : ILLMProvider
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(OpenAICompatibleProvider));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

        public async Task<string> SendAsync(List<ChatMessage> messages, ChatSettings settings)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentException("Messages cannot be empty.", nameof(messages));

            try
            {
                var body = new
                {
                    model = settings.Model,
                    messages = messages.ConvertAll(m => new { role = m.Role, content = m.Content }),
                    temperature = settings.Temperature,
                    max_tokens = settings.MaxTokens,
                };

                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.ApiUrl.TrimEnd('/')}/chat/completions")
                {
                    Content = content
                };

                if (!string.IsNullOrEmpty(settings.ApiKey))
                    request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");

                Logger.Debug("Sending chat request to {Url} with model {Model}", settings.ApiUrl, settings.Model);

                var response = await _http.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error("LLM API error {Status}: {Body}", response.StatusCode, responseBody);
                    return $"Sorry, I couldn't reach the AI. Error: {response.StatusCode} 🐱";
                }

                // Parse OpenAI-compatible response
                using var doc = JsonDocument.Parse(responseBody);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var text = choices[0].GetProperty("message").GetProperty("content").GetString();
                    Logger.Information("LLM response: {Text}", text?.Substring(0, Math.Min(100, text?.Length ?? 0)));
                    return text ?? "...";
                }

                return "...";
            }
            catch (TaskCanceledException)
            {
                Logger.Error("LLM request timed out");
                return "Hmm, the AI is taking too long... 🐱";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "LLM request failed");
                return $"Meow... something went wrong. {ex.Message}";
            }
        }
    }
}