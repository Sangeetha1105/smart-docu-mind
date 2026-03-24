using SmartDocuMind.Models;

namespace SmartDocuMind.Services
{
    public interface IFileProcessor
    {
        Task<string> ExtractTextAsync(string filePath);
        List<string> SplitIntoChunks(string text, int chunkSize = 2000);
    }
}