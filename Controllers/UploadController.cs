using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;

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
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFileAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            string text = "";
            int pageCount = 0;

            var allowedExtensions = new[] { ".pdf", ".txt" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest("Invalid file type. Only PDF or TXT allowed.");

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{file.FileName}");
            await using (var stream = new FileStream(tempPath, FileMode.Create))
                await file.CopyToAsync(stream);

            if (ext == ".txt")
                text = await System.IO.File.ReadAllTextAsync(tempPath);

            if (ext == ".pdf")
            {
                try
                {
                    using var document = UglyToad.PdfPig.PdfDocument.Open(tempPath);
                    pageCount = document.NumberOfPages;
                }
                catch
                {
                    pageCount = 0;
                }
            }

            return Ok(new
            {
                filename = file.FileName,
                length = file.Length,
                lines = string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length,
                pages = pageCount,
                tempPath
            });
        }
    }

}