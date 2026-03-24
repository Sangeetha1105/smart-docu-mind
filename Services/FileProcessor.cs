using SmartDocuMind.Models;
using UglyToad.PdfPig;

namespace SmartDocuMind.Services
{
    public class FileProcessor : IFileProcessor
    {
        public async Task<string> ExtractTextAsync(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string content = "";

            if (ext == ".txt")
            {
                content = await File.ReadAllTextAsync(filePath);
            }
            else if (ext == ".pdf")
            {
                using var doc = PdfDocument.Open(filePath);
                foreach (var page in doc.GetPages())
                {
                    var lines = page.GetWords()
                        .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                        .OrderBy(g => g.Key);

                    foreach (var line in lines)
                        content += string.Join(" ", line.Select(w => w.Text)) + "\n";
                }
            }
            else
            {
                throw new InvalidOperationException("Unsupported file type.");
            }

            return content;
        }

        public List<string> SplitIntoChunks(string text, int chunkSize = 2000)
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
    }
}