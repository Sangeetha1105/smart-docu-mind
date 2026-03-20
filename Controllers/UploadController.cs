using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Net.Http.Json;

namespace SmartDocuMind.Controllers
{
    public class QuestionRequest
    {
        public string FileId { get; set; } = default!;
        public string Question { get; set; } = default!;
    }

    public class Message
    {
        public string role { get; set; } = default!;
        public string content { get; set; } = default!;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        // =========================
        // In-memory stores
        // =========================
        private static readonly ConcurrentDictionary<string, List<string>> FileChunksStore = new();
        private static readonly ConcurrentDictionary<string, List<Message>> ChatHistoryStore = new();

        // =========================
        // Upload API
        // =========================
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var allowedExtensions = new[] { ".pdf", ".txt" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return BadRequest("Only PDF or TXT allowed.");

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{file.FileName}");
            await using (var stream = new FileStream(tempPath, FileMode.Create))
                await file.CopyToAsync(stream);

            string content = "";
            int lineCount = 0;

            try
            {
                if (ext == ".txt")
                {
                    content = await System.IO.File.ReadAllTextAsync(tempPath);
                    lineCount = string.IsNullOrEmpty(content) ? 0 :
                        content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                }
                else if (ext == ".pdf")
                {
                    using var document = PdfDocument.Open(tempPath);
                    foreach (var page in document.GetPages())
                    {
                        var lines = page.GetWords()
                            .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                            .OrderBy(g => g.Key);

                        foreach (var line in lines)
                            content += string.Join(" ", line.Select(w => w.Text)) + "\n";

                        lineCount += lines.Count();
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing file: {ex.Message}");
            }

            // =========================
            // Split into manageable chunks (preprocessed)
            // =========================
            var chunks = SplitTextIntoChunks(content, 2000);
            var fileId = Guid.NewGuid().ToString();
            FileChunksStore[fileId] = chunks;

            return Ok(new
            {
                fileId,
                fileName = file.FileName,
                file.Length,
                lines = lineCount,
                preview = content.Length > 300 ? content.Substring(0, 300) + "..." : content
            });
        }

        private List<string> SplitTextIntoChunks(string text, int chunkSize = 2000)
        {
            var chunks = new List<string>();
            int start = 0;
            while (start < text.Length)
            {
                int length = Math.Min(chunkSize, text.Length - start);
                chunks.Add(text.Substring(start, length));
                start += length;
            }
            return chunks;
        }

        // =========================
        // Ask API (fast retrieval + streaming)
        // =========================
        [HttpPost("ask-stream")]
        public async Task AskQuestionStreamAsync([FromBody] QuestionRequest request)
        {
            if (!FileChunksStore.TryGetValue(request.FileId, out var chunks))
            {
                Response.StatusCode = 404;
                await Response.WriteAsync("File not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Question))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Question cannot be empty.");
                return;
            }

            Response.ContentType = "text/plain";
            var httpClient = new HttpClient();

            // =========================
            // FAST keyword retrieval
            // Only pick 2–3 relevant chunks
            // =========================
            var questionLower = request.Question.ToLower();
            var relevantChunks = chunks
                .Where(c => c.IndexOf(questionLower, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(2)
                .Select(c => c.Length > 2000 ? c.Substring(0, 2000) : c)
                .ToList();

            if (!relevantChunks.Any())
                relevantChunks = chunks.Take(2).ToList(); // fallback

            var context = string.Join("\n", relevantChunks);

            // =========================
            // Chat memory
            // =========================
            if (!ChatHistoryStore.ContainsKey(request.FileId))
                ChatHistoryStore[request.FileId] = new List<Message>();

            var chatHistory = ChatHistoryStore[request.FileId];

            if (chatHistory.Count == 0)
            {
                chatHistory.Add(new Message
                {
                    role = "system",
                    content = $"You are a helpful assistant. Answer ONLY from the following document sections:\n{context}"
                });
            }

            chatHistory.Add(new Message
            {
                role = "user",
                content = request.Question
            });

            // =========================
            // Call Ollama streaming API
            // =========================
            var requestBody = new
            {
                model = "llama3",
                messages = chatHistory,
                stream = true
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/chat")
            {
                Content = JsonContent.Create(requestBody)
            };

            var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string fullAnswer = "";

            // =========================
            // Stream tokens immediately (async-safe)
            // =========================
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var token = doc.RootElement
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    if (!string.IsNullOrEmpty(token))
                    {
                        fullAnswer += token;

                        // send immediately to client
                        await Response.WriteAsync(token);
                        await Response.Body.FlushAsync();
                    }
                }
                catch
                {
                    // ignore malformed chunks
                }
            }

            // =========================
            // Save assistant response
            // =========================
            chatHistory.Add(new Message
            {
                role = "assistant",
                content = fullAnswer
            });

            if (chatHistory.Count > 20)
                chatHistory.RemoveAt(1); // keep system message
        }
    }
}