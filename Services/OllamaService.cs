using SmartDocuMind.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartDocuMind.Services
{
    public class OllamaService : IAIService
    {
        private readonly HttpClient _httpClient;

        public string ProviderName => "ollama";

        public OllamaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> StreamAnswerAsync(
            string model,
            List<Message> messages,
            HttpResponse response)
        {
            var requestBody = new
            {
                model = model,
                messages = messages.Select(m => new
                {
                    role = m.Role.ToLower(),
                    content = m.Content
                }),
                stream = true
            };

            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "http://localhost:11434/api/chat")
            {
                Content = JsonContent.Create(requestBody)
            };

            HttpResponseMessage aiResponse;

            try
            {
                aiResponse = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"OLLAMA_CONNECTION: Unable to connect to Ollama for model '{model}'.", ex);
            }

            if (aiResponse.StatusCode == HttpStatusCode.NotFound)
            {
                var notFoundContent = await aiResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"OLLAMA_MODEL_NOT_FOUND: Model '{model}' is not available. {notFoundContent}");
            }

            if (!aiResponse.IsSuccessStatusCode)
            {
                var errorContent = await aiResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"OLLAMA_ERROR: {(int)aiResponse.StatusCode} - {errorContent}");
            }

            using var stream = await aiResponse.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string fullAnswer = "";
            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);

                    var token = doc.RootElement
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        fullAnswer += token;

                        await response.WriteAsync(token);
                        await response.Body.FlushAsync();
                    }
                }
                catch
                {
                    // Ignore malformed chunks
                }
            }

            return fullAnswer;
        }
    }
}
