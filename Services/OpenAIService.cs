using SmartDocuMind.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartDocuMind.Services
{
    // Handles communication with OpenAI chat completions API
    public class OpenAIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        // Name used to match request.Provider
        public string ProviderName => "openai";

        public OpenAIService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> StreamAnswerAsync(
            string model,
            List<Message> messages,
            HttpResponse response)
        {
            // Read API key from appsettings.json
            var apiKey = _configuration["OpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OPENAI_CONFIG: OpenAI API key is missing.");

            // Prepare request body for OpenAI chat completions
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

            // Create HTTP request to OpenAI
            var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions")
            {
                Content = JsonContent.Create(requestBody)
            };

            // Set bearer token in request header
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            // Throw exception if API call fails
            var aiResponse = await _httpClient.SendAsync(
    httpRequest,
    HttpCompletionOption.ResponseHeadersRead);

            if (aiResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new InvalidOperationException("OPENAI_429");
            }

            if (aiResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("OPENAI_401");
            }

            if (!aiResponse.IsSuccessStatusCode)
            {
                var errorContent = await aiResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"OPENAI_ERROR: {(int)aiResponse.StatusCode} - {errorContent}");
            }

            using var stream = await aiResponse.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            // Store final full answer
            string fullAnswer = string.Empty;

            string? line;

            // OpenAI sends Server-Sent Events style lines starting with "data:"
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Ignore lines not starting with data:
                if (!line.StartsWith("data:"))
                    continue;

                // Remove "data:" prefix
                var json = line["data:".Length..].Trim();

                // OpenAI signals stream completion using [DONE]
                if (json == "[DONE]")
                    break;

                try
                {
                    // Parse JSON line
                    using var doc = JsonDocument.Parse(json);

                    var choices = doc.RootElement.GetProperty("choices");

                    if (choices.GetArrayLength() > 0)
                    {
                        var delta = choices[0].GetProperty("delta");

                        // Content may not always be present in every streamed event
                        if (delta.TryGetProperty("content", out var contentElement))
                        {
                            var token = contentElement.GetString();

                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                // Append token to final full answer
                                fullAnswer += token;

                                // Immediately stream token to client
                                await response.WriteAsync(token);
                                await response.Body.FlushAsync();
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore malformed chunks and continue
                }
            }

            // Return full answer so controller can store it
            return fullAnswer;
        }
    }
}
