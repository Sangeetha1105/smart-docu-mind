using Microsoft.AspNetCore.Http;
using SmartDocuMind.Models;
using SmartDocuMind.Services;
using System.Net;
using System.Text;

namespace SmartDocuMind.Tests.Services;

public class OllamaServiceTests
{
    [Fact]
    public async Task StreamAnswerAsync_WhenConnectionFails_ThrowsOllamaConnectionError()
    {
        var handler = new StubHttpMessageHandler
        {
            ExceptionToThrow = new HttpRequestException("connection failed")
        };
        var service = new OllamaService(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StreamAnswerAsync("llama3", CreateMessages(), CreateHttpResponse()));

        Assert.Contains("OLLAMA_CONNECTION", exception.Message);
        Assert.Contains("llama3", exception.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_WhenModelIsMissing_ThrowsModelNotFoundError()
    {
        var handler = new StubHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("model missing")
            }
        };
        var service = new OllamaService(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StreamAnswerAsync("mistral", CreateMessages(), CreateHttpResponse()));

        Assert.Contains("OLLAMA_MODEL_NOT_FOUND", exception.Message);
        Assert.Contains("mistral", exception.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_WhenApiReturnsOtherError_ThrowsOllamaError()
    {
        var handler = new StubHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server failure")
            }
        };
        var service = new OllamaService(new HttpClient(handler));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StreamAnswerAsync("llama3", CreateMessages(), CreateHttpResponse()));

        Assert.Equal("OLLAMA_ERROR: 500 - server failure", exception.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_WhenStreamSucceeds_WritesTokensAndReturnsFullAnswer()
    {
        var handler = new StubHttpMessageHandler
        {
            ResponseFactory = request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("http://localhost:11434/api/chat", request.RequestUri!.ToString());

                var body = string.Join('\n', new[]
                {
                    "{\"message\":{\"content\":\"Hello \"}}",
                    "{\"message\":{\"content\":\"from Ollama\"}}"
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            }
        };
        var service = new OllamaService(new HttpClient(handler));
        var response = CreateHttpResponse();

        var result = await service.StreamAnswerAsync("llama3", CreateMessages(), response);

        Assert.Equal("Hello from Ollama", result);
        Assert.Equal("Hello from Ollama", ReadResponseBody(response));
    }

    [Fact]
    public void ProviderName_ReturnsOllama()
    {
        var service = new OllamaService(new HttpClient(new StubHttpMessageHandler()));

        Assert.Equal("ollama", service.ProviderName);
    }

    private static List<Message> CreateMessages() =>
        new()
        {
            new Message
            {
                Role = "user",
                Content = "hello"
            }
        };

    private static HttpResponse CreateHttpResponse()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context.Response;
    }

    private static string ReadResponseBody(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; init; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        public Exception? ExceptionToThrow { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResponseFactory(request));
        }
    }
}
