using Microsoft.AspNetCore.Http;
using SmartDocuMind.Models;
using SmartDocuMind.Services;

namespace SmartDocuMind.Tests.Controllers;

public class UploadControllerAskStreamTests
{
    [Fact]
    public async Task AskQuestionStreamAsync_WhenRequestIsNull_WritesBadRequestToResponse()
    {
        var controller = UploadControllerTestSupport.CreateController();

        await controller.AskQuestionStreamAsync(null!);

        Assert.Equal(StatusCodes.Status400BadRequest, controller.Response.StatusCode);
        Assert.Equal("Request body is required.", UploadControllerTestSupport.ReadResponseBody(controller));
    }

    [Fact]
    public async Task AskQuestionStreamAsync_WhenFileIdIsMissing_WritesBadRequestToResponse()
    {
        var controller = UploadControllerTestSupport.CreateController();

        await controller.AskQuestionStreamAsync(new QuestionRequest
        {
            FileId = "",
            Question = "What is this document about?"
        });

        Assert.Equal(StatusCodes.Status400BadRequest, controller.Response.StatusCode);
        Assert.Equal("FileId is required.", UploadControllerTestSupport.ReadResponseBody(controller));
    }

    [Fact]
    public async Task AskQuestionStreamAsync_WhenQuestionIsEmpty_WritesBadRequestToResponse()
    {
        var controller = UploadControllerTestSupport.CreateController();

        await controller.AskQuestionStreamAsync(new QuestionRequest
        {
            FileId = "file-1",
            Question = " "
        });

        Assert.Equal(StatusCodes.Status400BadRequest, controller.Response.StatusCode);
        Assert.Equal("Question cannot be empty.", UploadControllerTestSupport.ReadResponseBody(controller));
    }

    [Fact]
    public async Task AskQuestionStreamAsync_WhenFileChunksDoNotExist_WritesNotFoundToResponse()
    {
        var controller = UploadControllerTestSupport.CreateController(
            chatMemoryService: new UploadControllerTestSupport.StubChatMemoryService());

        await controller.AskQuestionStreamAsync(new QuestionRequest
        {
            FileId = "missing-file",
            Question = "What is this document about?"
        });

        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        Assert.Equal("File not found.", UploadControllerTestSupport.ReadResponseBody(controller));
    }

    [Fact]
    public async Task AskQuestionStreamAsync_WhenFastModeSucceeds_WritesAnswerAndStoresConversation()
    {
        var chatMemoryService = new UploadControllerTestSupport.StubChatMemoryService();
        chatMemoryService.SaveFileChunks("file-1", new List<string>
        {
            "This document explains invoice processing.",
            "This section is unrelated."
        });

        var aiService = new UploadControllerTestSupport.FakeAiService("ollama")
        {
            ResponseText = "Here is the answer from the document."
        };

        var controller = UploadControllerTestSupport.CreateController(
            chatMemoryService: chatMemoryService,
            aiServices: new[] { aiService });

        await controller.AskQuestionStreamAsync(new QuestionRequest
        {
            FileId = "file-1",
            Question = "invoice",
            AiMode = AiMode.Fast
        });

        var responseBody = UploadControllerTestSupport.ReadResponseBody(controller);
        var history = chatMemoryService.GetOrCreateHistory("file-1");

        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
        Assert.Equal("text/plain", controller.Response.ContentType);
        Assert.Contains("Here is the answer from the document.", responseBody);
        Assert.Equal("llama3", aiService.LastModel);
        Assert.Equal(3, history.Count);
        Assert.Equal("system", history[0].Role);
        Assert.Contains("invoice processing", history[0].Content);
        Assert.Equal("user", history[1].Role);
        Assert.Equal("invoice", history[1].Content);
        Assert.Equal("assistant", history[2].Role);
        Assert.Equal("Here is the answer from the document.", history[2].Content);
    }

    [Fact]
    public async Task AskQuestionStreamAsync_WhenAdvancedModeFails_FallsBackToBalancedMode()
    {
        var chatMemoryService = new UploadControllerTestSupport.StubChatMemoryService();
        chatMemoryService.SaveFileChunks("file-2", new List<string>
        {
            "The reimbursement policy covers travel expenses.",
            "Employees must keep receipts."
        });

        var openAiService = new UploadControllerTestSupport.FakeAiService("openai")
        {
            ExceptionToThrow = new InvalidOperationException("OPENAI_429")
        };
        var ollamaService = new UploadControllerTestSupport.FakeAiService("ollama")
        {
            ResponseText = "Balanced answer from fallback model."
        };

        var controller = UploadControllerTestSupport.CreateController(
            chatMemoryService: chatMemoryService,
            aiServices: new IAIService[] { openAiService, ollamaService });

        await controller.AskQuestionStreamAsync(new QuestionRequest
        {
            FileId = "file-2",
            Question = "travel",
            AiMode = AiMode.Advanced
        });

        var responseBody = UploadControllerTestSupport.ReadResponseBody(controller);
        var history = chatMemoryService.GetOrCreateHistory("file-2");

        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
        Assert.Contains("Advanced mode failed: OpenAI quota or rate limit was exceeded.", responseBody);
        Assert.Contains("Switching to Balanced mode", responseBody);
        Assert.Contains("Balanced answer from fallback model.", responseBody);
        Assert.Equal("gpt-4o-mini", openAiService.LastModel);
        Assert.Equal("mistral", ollamaService.LastModel);
        Assert.Equal("assistant", history.Last().Role);
        Assert.Equal("Balanced answer from fallback model.", history.Last().Content);
    }
}
