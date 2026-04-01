using SmartDocuMind.Models;
using System.Collections.Concurrent;

namespace SmartDocuMind.Services
{
    public class ChatMemoryService : IChatMemoryService
    {
        private readonly ConcurrentDictionary<string, List<string>> _fileChunks = new();
        private readonly ConcurrentDictionary<string, List<Message>> _chatHistory = new();

        public List<Message> GetOrCreateHistory(string fileId)
        {
            return _chatHistory.GetOrAdd(fileId, _ => new List<Message>());
        }

        public void SaveMessage(string fileId, Message message)
        {
            var history = GetOrCreateHistory(fileId);
            history.Add(message);

            if (history.Count > 20)
                history.RemoveAt(1); // keep system message
        }

        public List<string> GetFileChunks(string fileId)
        {
            return _fileChunks.TryGetValue(fileId, out var chunks) ? chunks : new List<string>();
        }

        public void SaveFileChunks(string fileId, List<string> chunks)
        {
            _fileChunks[fileId] = chunks;
        }
    }
}