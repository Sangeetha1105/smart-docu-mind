using SmartDocuMind.Models;
using SmartDocuMind.Services;

namespace SmartDocuMind.Tests.Services;

public class ChatMemoryServiceTests
{
    [Fact]
    public void GetFileChunks_WhenFileIdDoesNotExist_ReturnsEmptyList()
    {
        var service = new ChatMemoryService();

        var result = service.GetFileChunks("missing-file");

        Assert.Empty(result);
    }

    [Fact]
    public void SaveFileChunks_WhenCalled_StoresChunksForThatFileId()
    {
        var service = new ChatMemoryService();
        var chunks = new List<string> { "chunk-1", "chunk-2" };

        service.SaveFileChunks("file-1", chunks);

        var result = service.GetFileChunks("file-1");

        Assert.Equal(chunks, result);
    }

    [Fact]
    public void GetOrCreateHistory_WhenCalledTwiceForSameFileId_ReturnsSameHistoryList()
    {
        var service = new ChatMemoryService();

        var firstHistory = service.GetOrCreateHistory("file-1");
        firstHistory.Add(new Message
        {
            Role = "user",
            Content = "hello"
        });

        var secondHistory = service.GetOrCreateHistory("file-1");

        Assert.Same(firstHistory, secondHistory);
        Assert.Single(secondHistory);
        Assert.Equal("hello", secondHistory[0].Content);
    }

    [Fact]
    public void SaveMessage_WhenHistoryExceedsTwenty_PreservesSystemMessageAndRemovesOldestNonSystemMessage()
    {
        var service = new ChatMemoryService();
        const string fileId = "file-1";

        service.SaveMessage(fileId, new Message
        {
            Role = "system",
            Content = "system prompt"
        });

        for (var index = 1; index <= 20; index++)
        {
            service.SaveMessage(fileId, new Message
            {
                Role = "user",
                Content = $"message-{index}"
            });
        }

        var history = service.GetOrCreateHistory(fileId);

        Assert.Equal(20, history.Count);
        Assert.Equal("system", history[0].Role);
        Assert.Equal("system prompt", history[0].Content);
        Assert.DoesNotContain(history, message => message.Content == "message-1");
        Assert.Contains(history, message => message.Content == "message-20");
    }
}
