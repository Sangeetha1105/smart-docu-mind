using SmartDocuMind.Models;

namespace SmartDocuMind.Services
{
    // Service contract for storing file chunks and chat history in memory
    public interface IChatMemoryService
    {
        // Save chunks for a file id
        void SaveFileChunks(string fileId, List<string> chunks);

        // Get chunks for a file id
        List<string> GetFileChunks(string fileId);

        // Get existing chat history or create a new one
        List<Message> GetOrCreateHistory(string fileId);

        // Save one message into history
        void SaveMessage(string fileId, Message message);
    }
}