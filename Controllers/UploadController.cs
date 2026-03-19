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

    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        // In-memory storage
        private static readonly ConcurrentDictionary<string, string> FileContentStore = new();

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
        [HttpPost("ask")]
        public async Task<IActionResult> AskQuestionAsync([FromBody] QuestionRequest request)
        {
            if (!FileContentStore.TryGetValue(request.FileId, out var content))
                return NotFound("File not found or expired.");

            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest("Question cannot be empty.");

            try
            {
                var httpClient = new HttpClient();

                // Limit content size (VERY IMPORTANT)
                var shortContent = content.Length > 3000
                    ? content.Substring(0, 3000)
                    : content;

                var prompt = $@"
You are a helpful assistant.
Answer ONLY from the document.

Document:
{shortContent}

Question:
{request.Question}
";

                var requestBody = new
                {
                    model = "llama3",
                    prompt = prompt,
                    stream = true
                };

                var response = await httpClient.PostAsJsonAsync(
                    "http://localhost:11434/api/generate",
                    requestBody
                );

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();

                return Ok(new
                {
                    request.Question,
                    answer = result?.response
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"AI Error: {ex.Message}");
            }
        }

        // Helper class
        public class OllamaResponse
        {
            public string response { get; set; }
        }
    }

}