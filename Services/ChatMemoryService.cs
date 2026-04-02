using SmartDocuMind.Models;
using System.Collections.Concurrent;

namespace SmartDocuMind.Services
{
    // In-memory storage for uploaded file chunks and chat history
    public class ChatMemoryService : IChatMemoryService
    {
        // Stores fileId -> list of text chunks
        private readonly ConcurrentDictionary<string, List<string>> _fileChunksStore = new();

        // Stores fileId -> chat history
        private readonly ConcurrentDictionary<string, List<Message>> _chatHistoryStore = new();

        public void SaveFileChunks(string fileId, List<string> chunks)
        {
            _fileChunksStore[fileId] = chunks;
        }

        public List<string> GetFileChunks(string fileId)
        {
            return _fileChunksStore.TryGetValue(fileId, out var chunks)
                ? chunks
                : new List<string>();
        }

        public List<Message> GetOrCreateHistory(string fileId)
        {
            return _chatHistoryStore.GetOrAdd(fileId, _ => new List<Message>());
        }

        public void SaveMessage(string fileId, Message message)
        {
            var history = GetOrCreateHistory(fileId);
            history.Add(message);

            // Keep history size under control
            // Preserve the first system message if possible
            if (history.Count > 20)
            {
                history.RemoveAt(1);
            }
        }
    }
}