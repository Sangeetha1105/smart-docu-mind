using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartDocuMind.Controllers;
using SmartDocuMind.Models;
using SmartDocuMind.Services;
using System.Text;

namespace SmartDocuMind.Tests.Controllers;

internal static class UploadControllerTestSupport
{
    internal static IFormFile CreateFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    internal static UploadController CreateController(
        IFileProcessor? fileProcessor = null,
        IChatMemoryService? chatMemoryService = null,
        IEnumerable<IAIService>? aiServices = null)
    {
        var controller = new UploadController(
            fileProcessor ?? new StubFileProcessor(),
            chatMemoryService ?? new StubChatMemoryService(),
            aiServices ?? Array.Empty<IAIService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Response =
                {
                    Body = new MemoryStream()
                }
            }
        };

        return controller;
    }

    internal static string ReadResponseBody(ControllerBase controller)
    {
        controller.Response.Body.Position = 0;
        using var reader = new StreamReader(controller.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    internal sealed class StubFileProcessor : IFileProcessor
    {
        public string ExtractedText { get; init; } = string.Empty;

        public List<string> ChunksToReturn { get; init; } = new();

        public int LineCountToReturn { get; init; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<string> ExtractTextAsync(string filePath)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ExtractedText);
        }

        public List<string> SplitIntoChunks(string text, int chunkSize = 2000) => ChunksToReturn;

        public int CountLines(string text) => LineCountToReturn;
    }

    internal sealed class StubChatMemoryService : IChatMemoryService
    {
        private readonly Dictionary<string, List<string>> _fileChunks = new();
        private readonly Dictionary<string, List<Message>> _history = new();

        public string? SavedFileId { get; private set; }

        public List<string>? SavedChunks { get; private set; }

        public void SaveFileChunks(string fileId, List<string> chunks)
        {
            SavedFileId = fileId;
            SavedChunks = chunks;
            _fileChunks[fileId] = chunks;
        }

        public List<string> GetFileChunks(string fileId) =>
            _fileChunks.TryGetValue(fileId, out var chunks) ? chunks : new List<string>();

        public List<Message> GetOrCreateHistory(string fileId)
        {
            if (!_history.TryGetValue(fileId, out var messages))
            {
                messages = new List<Message>();
                _history[fileId] = messages;
            }

            return messages;
        }

        public void SaveMessage(string fileId, Message message)
        {
            GetOrCreateHistory(fileId).Add(message);
        }
    }

    internal sealed class FakeAiService(string providerName) : IAIService
    {
        public string ProviderName => providerName;

        public string ResponseText { get; init; } = "default answer";

        public Exception? ExceptionToThrow { get; init; }

        public string? LastModel { get; private set; }

        public async Task<string> StreamAnswerAsync(string model, List<Message> messages, HttpResponse response)
        {
            LastModel = model;

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            await response.WriteAsync(ResponseText);
            await response.Body.FlushAsync();
            return ResponseText;
        }
    }
}
