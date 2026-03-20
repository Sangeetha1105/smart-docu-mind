using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Collections.Concurrent;

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
        // In-memory storage
        private static readonly ConcurrentDictionary<string, string> FileContentStore = new();

        private static readonly ConcurrentDictionary<string, List<Message>> ChatHistoryStore 
    = new();

        // =========================
        // 📌 Upload API
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
                return BadRequest("Invalid file type. Only PDF or TXT allowed.");

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{file.FileName}");

            await using (var stream = new FileStream(tempPath, FileMode.Create))
                await file.CopyToAsync(stream);

            string content = "";
            int pageCount = 0;
            int lineCount = 0;

            try
            {
                // ================= TXT =================
                if (ext == ".txt")
                {
                    content = await System.IO.File.ReadAllTextAsync(tempPath);
                    lineCount = string.IsNullOrEmpty(content)
                        ? 0
                        : content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                }
                // ================= PDF =================
                else if (ext == ".pdf")
                {
                    using var document = PdfDocument.Open(tempPath);
                    pageCount = document.NumberOfPages;

                    foreach (var page in document.GetPages())
                    {
                        // Group words by approximate line (bottom coordinate)
                        var lines = page.GetWords()
                            .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                            .OrderBy(g => g.Key);

                        foreach (var line in lines)
                        {
                            content += string.Join(" ", line.Select(w => w.Text)) + "\n";
                        }

                        lineCount += lines.Count();
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing file: {ex.Message}");
            }

            // Store content with FileId
            var fileId = Guid.NewGuid().ToString();
            FileContentStore[fileId] = content;

            return Ok(new
            {
                fileId,
                fileName = file.FileName,
                file.Length,
                lines = lineCount,
                pages = pageCount,
                preview = content.Length > 300 ? content.Substring(0, 300) + "..." : content
            });
        }

        // =========================
        // 🤖 Ask API
        // =========================
        [HttpPost("ask-stream")]
        public async Task AskQuestionStreamAsync([FromBody] QuestionRequest request)
        {
            if (!FileContentStore.TryGetValue(request.FileId, out var content))
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
            // Init chat history
            // =========================
            if (!ChatHistoryStore.ContainsKey(request.FileId))
            {
                ChatHistoryStore[request.FileId] = new List<Message>();
            }

            var chatHistory = ChatHistoryStore[request.FileId];

            // Add system message once
            if (chatHistory.Count == 0)
            {
                var shortContent = content.Length > 3000
                    ? content.Substring(0, 3000)
                    : content;

                chatHistory.Add(new Message
                {
                    role = "system",
                    content = $"You are a helpful assistant. Answer ONLY from this document:\n{shortContent}"
                });
            }

            // Add user question
            chatHistory.Add(new Message
            {
                role = "user",
                content = request.Question
            });

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

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var chunk = System.Text.Json.JsonSerializer.Deserialize<OllamaStreamChunk>(line);

                    var token = chunk?.message?.content;

                    if (!string.IsNullOrEmpty(token))
                    {
                        fullAnswer += token;

                        // Send token immediately to client
                        await Response.WriteAsync(token);
                        await Response.Body.FlushAsync();
                    }
                }
                catch
                {
                    // ignore malformed chunks
                }
            }

            // Save assistant full response
            chatHistory.Add(new Message
            {
                role = "assistant",
                content = fullAnswer
            });

            // Limit memory
            if (chatHistory.Count > 20)
            {
                chatHistory.RemoveAt(1);
            }
        }
        public class OllamaStreamChunk
{
    public Message message { get; set; }
}
    }

}