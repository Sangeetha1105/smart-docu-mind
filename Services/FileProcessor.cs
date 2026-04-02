using UglyToad.PdfPig;

namespace SmartDocuMind.Services
{
    // Concrete implementation of file processing service
    public class FileProcessor : IFileProcessor
    {
        public async Task<string> ExtractTextAsync(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string content = string.Empty;

            // Handle text files
            if (ext == ".txt")
            {
                content = await File.ReadAllTextAsync(filePath);
            }
            // Handle PDF files
            else if (ext == ".pdf")
            {
                using var document = PdfDocument.Open(filePath);

                foreach (var page in document.GetPages())
                {
                    // Group words by vertical position to rebuild lines
                    var lines = page.GetWords()
                        .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                        .OrderBy(g => g.Key);

                    foreach (var line in lines)
                    {
                        content += string.Join(" ", line.Select(w => w.Text)) + Environment.NewLine;
                    }
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

            // Return empty list if text is empty
            if (string.IsNullOrWhiteSpace(text))
                return chunks;

            int start = 0;

            // Split text into fixed-size chunks
            while (start < text.Length)
            {
                int length = Math.Min(chunkSize, text.Length - start);
                chunks.Add(text.Substring(start, length));
                start += length;
            }

            return chunks;
        }

        public int CountLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}