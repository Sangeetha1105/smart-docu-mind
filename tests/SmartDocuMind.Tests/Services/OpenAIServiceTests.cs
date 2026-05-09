using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartDocuMind.Models;
using SmartDocuMind.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace SmartDocuMind.Tests.Services;

public class OpenAIServiceTests
{
    [Fact]
    public async Task StreamAnswerAsync_WhenApiKeyIsMissing_ThrowsInvalidOperationException()
    {
        var service = new OpenAIService(
            new HttpClient(new StubHttpMessageHandler()),
            CreateConfiguration(null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StreamAnswerAsync("gpt-4o-mini", CreateMessages(), CreateHttpResponse()));

        Assert.Equal("OPENAI_CONFIG: OpenAI API key is missing.", exception.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_WhenApiReturnsUnauthorized_ThrowsOpenAi401()
    {
        var handler = new StubHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        };
        var service = new OpenAIService(new HttpClient(handler), CreateConfiguration("test-key"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StreamAnswerAsync("gpt-4o-mini", CreateMessages(), CreateHttpResponse()));

        Assert.Equal("OPENAI_401", exception.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_WhenApiReturnsTooManyRequests_ThrowsOpenAi429()
    {
        var handler = new StubHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        };
        var service = new OpenAIService(new HttpClient(handler), CreateConfiguration("test-key"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StreamAnswerAsync("gpt-4o-mini", CreateMessages(), CreateHttpResponse()));

        Assert.Equal("OPENAI_429", exception.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_WhenApiReturnsOtherError_ThrowsOpenAiErrorWithStatusCode()
    {
        var handler = new StubHttpMessageHandler
        {
            ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad request body")
            }
        };
        var service = new OpenAIService(new HttpClient(handler), CreateConfiguration("test-key"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StreamAnswerAsync("gpt-4o-mini", CreateMessages(), CreateHttpResponse()));

        Assert.Equal("OPENAI_ERROR: 400 - bad request body", exception.Message);
    }

    [Fact]
    public async Task StreamAnswerAsync_WhenStreamSucceeds_WritesTokensAndReturnsFullAnswer()
    {
        var handler = new StubHttpMessageHandler
        {
            ResponseFactory = request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://api.openai.com/v1/chat/completions", request.RequestUri!.ToString());
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("test-key", request.Headers.Authorization?.Parameter);

                var body = string.Join('\n', new[]
                {
                    "data: {\"choices\":[{\"delta\":{\"content\":\"Hello \"}}]}",
                    "data: {\"choices\":[{\"delta\":{\"content\":\"world\"}}]}",
                    "data: [DONE]"
                });

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "text/plain")
                };
            }
        };
        var service = new OpenAIService(new HttpClient(handler), CreateConfiguration("test-key"));
        var response = CreateHttpResponse();

        var result = await service.StreamAnswerAsync("gpt-4o-mini", CreateMessages(), response);

        Assert.Equal("Hello world", result);
        Assert.Equal("Hello world", ReadResponseBody(response));
    }

    [Fact]
    public void ProviderName_ReturnsOpenAi()
    {
        var service = new OpenAIService(
            new HttpClient(new StubHttpMessageHandler()),
            CreateConfiguration("test-key"));

        Assert.Equal("openai", service.ProviderName);
    }

    private static IConfiguration CreateConfiguration(string? apiKey)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = apiKey
            })
            .Build();
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ResponseFactory(request));
        }
    }
}
