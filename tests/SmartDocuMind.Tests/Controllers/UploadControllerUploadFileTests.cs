using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartDocuMind.Models;

namespace SmartDocuMind.Tests.Controllers;

public class UploadControllerUploadFileTests
{
    [Fact]
    public async Task UploadFileAsync_WhenFileIsNull_ReturnsBadRequest()
    {
        var controller = UploadControllerTestSupport.CreateController();

        var result = await controller.UploadFileAsync(null!);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded.", badRequestResult.Value);
    }

    [Fact]
    public async Task UploadFileAsync_WhenFileExtensionIsUnsupported_ReturnsBadRequest()
    {
        var controller = UploadControllerTestSupport.CreateController();
        var file = UploadControllerTestSupport.CreateFormFile("notes.docx", "sample content");

        var result = await controller.UploadFileAsync(file);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Only PDF or TXT files are allowed.", badRequestResult.Value);
    }

    [Fact]
    public async Task UploadFileAsync_WhenTextFileIsValid_ReturnsUploadResultAndStoresChunks()
    {
        var fileProcessor = new UploadControllerTestSupport.StubFileProcessor
        {
            ExtractedText = "first line\nsecond line",
            ChunksToReturn = new List<string> { "chunk-1", "chunk-2" },
            LineCountToReturn = 2
        };
        var chatMemoryService = new UploadControllerTestSupport.StubChatMemoryService();
        var controller = UploadControllerTestSupport.CreateController(fileProcessor, chatMemoryService);
        var file = UploadControllerTestSupport.CreateFormFile("notes.txt", "uploaded file content");

        var result = await controller.UploadFileAsync(file);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var uploadResult = Assert.IsType<UploadResult>(okResult.Value);

        Assert.Equal("notes.txt", uploadResult.FileName);
        Assert.Equal(file.Length, uploadResult.FileSize);
        Assert.Equal(2, uploadResult.Lines);
        Assert.Equal("first line\nsecond line", uploadResult.Preview);
        Assert.False(string.IsNullOrWhiteSpace(uploadResult.FileId));
        Assert.Equal(uploadResult.FileId, chatMemoryService.SavedFileId);
        Assert.Equal(fileProcessor.ChunksToReturn, chatMemoryService.SavedChunks);
    }

    [Fact]
    public async Task UploadFileAsync_WhenTextExtractionFails_ReturnsBadRequest()
    {
        var fileProcessor = new UploadControllerTestSupport.StubFileProcessor
        {
            ExceptionToThrow = new InvalidOperationException("sample extraction failure")
        };
        var controller = UploadControllerTestSupport.CreateController(fileProcessor);
        var file = UploadControllerTestSupport.CreateFormFile("notes.txt", "uploaded file content");

        var result = await controller.UploadFileAsync(file);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Error processing file: sample extraction failure", badRequestResult.Value);
    }
}
