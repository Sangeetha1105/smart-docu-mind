using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using System.Collections.Concurrent;

namespace SmartDocuMind.Controllers
{
    public class FileUploadRequest
    {
        public IFormFile File { get; set; } = default!;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private static ConcurrentDictionary<string, string> FileContentStore = new();
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
                if (ext == ".txt")
                {
                    content = await System.IO.File.ReadAllTextAsync(tempPath);
                    lineCount = string.IsNullOrEmpty(content)
                        ? 0
                        : content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
                }
                else if (ext == ".pdf")
                {
                    using var document = PdfDocument.Open(tempPath);
                    pageCount = document.NumberOfPages;

                    foreach (var page in document.GetPages())
                    {
                        // Group words by vertical position (Bottom) to count visual lines
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
                return BadRequest($"Failed to process file: {ex.Message}");
            }
            finally
            {
                // Optional: delete temp file after processing
                // System.IO.File.Delete(tempPath);
            }

            var fileId = Guid.NewGuid().ToString();
            FileContentStore[fileId] = content;

            return Ok(new
            {
                filename = file.FileName,
                length = file.Length,
                lines = lineCount,
                pages = pageCount,
                tempPath,
                fileId,
                contentPreview = content.Length > 500 ? content.Substring(0, 500) + "..." : content
            });
        }
    }
}
