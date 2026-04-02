namespace SmartDocuMind.Services
{
    // Service contract for file reading and chunk splitting
    public interface IFileProcessor
    {
        // Extracts text content from the given file path
        Task<string> ExtractTextAsync(string filePath);

        // Splits long text into smaller chunks
        List<string> SplitIntoChunks(string text, int chunkSize = 2000);

        // Counts lines in the extracted text
        int CountLines(string text);
    }
}