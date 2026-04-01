using SmartDocuMind.Models;

namespace SmartDocuMind.Services
{
    public interface IChatMemoryService
    {
        List<Message> GetOrCreateHistory(string fileId);
        void SaveMessage(string fileId, Message message);
        List<string> GetFileChunks(string fileId);
        void SaveFileChunks(string fileId, List<string> chunks);
    }
}