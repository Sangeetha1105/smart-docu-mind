using Microsoft.AspNetCore.Mvc;
using SmartDocuMind.Models;
using SmartDocuMind.Services;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartDocuMind.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IFileProcessor _fileProcessor;
        private readonly IChatMemoryService _chatMemory;
        private readonly HttpClient _httpClient;

        public UploadController(IFileProcessor fileProcessor, IChatMemoryService chatMemory, HttpClient httpClient)
        {
            _fileProcessor = fileProcessor;
            _chatMemory = chatMemory;
            _httpClient = httpClient;
        }

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

            string content;
            try { content = await _fileProcessor.ExtractTextAsync(tempPath); }
            catch (Exception ex) { return BadRequest($"Error processing file: {ex.Message}"); }

            var chunks = _fileProcessor.SplitIntoChunks(content, 2000);
            var fileId = Guid.NewGuid().ToString();
            _chatMemory.SaveFileChunks(fileId, chunks);

            int lineCount = string.IsNullOrEmpty(content) ? 0 : content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

            return Ok(new UploadResult
            {
                FileId = fileId,
                FileName = file.FileName,
                FileSize = file.Length,
                Lines = lineCount,
                Preview = content.Length > 300 ? content[..300] + "..." : content
            });
        }

        [HttpPost("ask-stream")]
        public async Task AskQuestionStreamAsync([FromBody] QuestionRequest request)
        {
            var chunks = _chatMemory.GetFileChunks(request.FileId);
            if (!chunks.Any())
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

            var relevantChunks = chunks
                .Where(c => c.Contains(request.Question, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            if (!relevantChunks.Any())
                relevantChunks = chunks.Take(2).ToList();

            var context = string.Join("\n", relevantChunks);

            var history = _chatMemory.GetOrCreateHistory(request.FileId);
            if (!history.Any())
            {
                history.Add(new Message
                {
                    Role = "system",
                    Content = $"You are a helpful assistant. Answer ONLY from the following document sections:\n{context}"
                });
            }

            history.Add(new Message { Role = "user", Content = request.Question });

            var requestBody = new { model = "llama3", messages = history, stream = true };
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/chat")
            {
                Content = JsonContent.Create(requestBody)
            };

            var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string fullAnswer = "";
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var token = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        fullAnswer += token;
                        await Response.WriteAsync(token);
                        await Response.Body.FlushAsync();
                    }
                }
                catch { }
            }

            _chatMemory.SaveMessage(request.FileId, new Message { Role = "assistant", Content = fullAnswer });
        }
    }
}