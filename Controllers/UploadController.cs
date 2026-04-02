using Microsoft.AspNetCore.Mvc;
using SmartDocuMind.Models;
using SmartDocuMind.Services;

namespace SmartDocuMind.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        // Service for file reading and chunk splitting
        private readonly IFileProcessor _fileProcessor;

        // Service for in-memory file chunks and chat history
        private readonly IChatMemoryService _chatMemoryService;

        // All registered AI services come here through DI
        private readonly IEnumerable<IAIService> _aiServices;

        public UploadController(
            IFileProcessor fileProcessor,
            IChatMemoryService chatMemoryService,
            IEnumerable<IAIService> aiServices)
        {
            _fileProcessor = fileProcessor;
            _chatMemoryService = chatMemoryService;
            _aiServices = aiServices;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFileAsync(IFormFile file)
        {
            // Validate uploaded file
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // Allow only PDF and TXT files
            var allowedExtensions = new[] { ".pdf", ".txt" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return BadRequest("Only PDF or TXT files are allowed.");

            // Save uploaded file temporarily into temp folder
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid()}_{file.FileName}");

            await using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            string content;

            try
            {
                // Extract text from uploaded file
                content = await _fileProcessor.ExtractTextAsync(tempPath);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error processing file: {ex.Message}");
            }
            finally
            {
                // Delete temp file after processing
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }

            // Split extracted text into chunks
            var chunks = _fileProcessor.SplitIntoChunks(content);

            // Create unique id for this uploaded file
            var fileId = Guid.NewGuid().ToString();

            // Store chunks in memory using file id
            _chatMemoryService.SaveFileChunks(fileId, chunks);

            // Count number of lines in extracted content
            var lineCount = _fileProcessor.CountLines(content);

            // Return upload result to user
            return Ok(new UploadResult
            {
                FileId = fileId,
                FileName = file.FileName,
                FileSize = file.Length,
                Lines = lineCount,
                Preview = content.Length > 300
                    ? content[..300] + "..."
                    : content
            });
        }

        [HttpPost("ask-stream")]
        public async Task AskQuestionStreamAsync([FromBody] QuestionRequest request)
        {
            // Validate request body
            if (request == null)
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Request body is required.");
                return;
            }

            // Validate file id
            if (string.IsNullOrWhiteSpace(request.FileId))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("FileId is required.");
                return;
            }

            // Validate question
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Question cannot be empty.");
                return;
            }

            // Get stored chunks for this file
            var chunks = _chatMemoryService.GetFileChunks(request.FileId);

            // Return 404 if file id not found
            if (!chunks.Any())
            {
                Response.StatusCode = 404;
                await Response.WriteAsync("File not found.");
                return;
            }

            // Set response type to plain text because tokens are streamed directly
            Response.ContentType = "text/plain";

            // Basic retrieval without RAG:
            // find chunks containing the question text
            var relevantChunks = chunks
                .Where(chunk => chunk.Contains(request.Question, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            // Fallback to first 2 chunks if no direct match is found
            if (!relevantChunks.Any())
            {
                relevantChunks = chunks.Take(2).ToList();
            }

            // Combine relevant chunks into one context string
            var context = string.Join(Environment.NewLine, relevantChunks);

            // Get or create history for this file
            var chatHistory = _chatMemoryService.GetOrCreateHistory(request.FileId);

            // Add system prompt only once when chat starts
            if (!chatHistory.Any())
            {
                _chatMemoryService.SaveMessage(request.FileId, new Message
                {
                    Role = "system",
                    Content = $"You are a helpful assistant. Answer ONLY from the following document sections:{Environment.NewLine}{context}"
                });

                // Re-read updated history after adding system message
                chatHistory = _chatMemoryService.GetOrCreateHistory(request.FileId);
            }

            // Save current user question into chat history
            _chatMemoryService.SaveMessage(request.FileId, new Message
            {
                Role = "user",
                Content = request.Question
            });

            // Re-read history so latest user message is included
            chatHistory = _chatMemoryService.GetOrCreateHistory(request.FileId);

            var requestedMode = request.AiMode ?? AiMode.Fast;
            var fallbackModes = GetFallbackModes(requestedMode).ToList();
            Exception? lastError = null;

            for (var index = 0; index < fallbackModes.Count; index++)
            {
                var currentMode = fallbackModes[index];
                var (provider, model) = ResolveAiSettings(currentMode);

                var aiService = _aiServices.FirstOrDefault(service =>
                    service.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

                if (aiService == null)
                {
                    lastError = new InvalidOperationException(
                        $"No AI service found for mode '{GetModeLabel(currentMode)}'.");
                    continue;
                }

                try
                {
                    if (index > 0)
                    {
                        await Response.WriteAsync(
                            $"\n\n{GetModeLabel(fallbackModes[index - 1])} mode is unavailable. Switching to {GetModeLabel(currentMode)} mode...\n\n");
                        await Response.Body.FlushAsync();
                    }

                    var fullAnswer = await aiService.StreamAnswerAsync(model, chatHistory, Response);

                    _chatMemoryService.SaveMessage(request.FileId, new Message
                    {
                        Role = "assistant",
                        Content = fullAnswer
                    });

                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;

                    await Response.WriteAsync(
                        $"\n\n{GetModeLabel(currentMode)} mode failed: {GetFailureReason(ex, currentMode)}\n");
                    await Response.Body.FlushAsync();
                }
            }

            Response.StatusCode = 503;
            await Response.WriteAsync(
                $"\nAI processing failed after trying fallback modes. Last error: {GetFailureReason(lastError, fallbackModes.Last())}");
        }

        // Converts a simple user-friendly AI mode into technical provider and model values
        private static (string Provider, string Model) ResolveAiSettings(AiMode? aiMode)
        {
            return aiMode switch
            {
                AiMode.Fast => ("ollama", "llama3"),
                AiMode.Balanced => ("ollama", "mistral"),
                AiMode.Advanced => ("openai", "gpt-4o-mini"),
                _ => ("ollama", "llama3") // default
            };
        }

        private static IEnumerable<AiMode> GetFallbackModes(AiMode aiMode)
        {
            yield return aiMode;

            if (aiMode == AiMode.Advanced)
            {
                yield return AiMode.Balanced;
                yield return AiMode.Fast;
            }
            else if (aiMode == AiMode.Balanced)
            {
                yield return AiMode.Fast;
            }
        }

        private static string GetModeLabel(AiMode aiMode)
        {
            return aiMode switch
            {
                AiMode.Fast => "Fast",
                AiMode.Balanced => "Balanced",
                AiMode.Advanced => "Advanced",
                _ => aiMode.ToString()
            };
        }

        private static string GetFailureReason(Exception? ex, AiMode aiMode)
        {
            if (ex == null)
            {
                return "Unknown error.";
            }

            var message = ex.Message;

            if (message.Contains("OPENAI_CONFIG", StringComparison.OrdinalIgnoreCase))
            {
                return "OpenAI API key is not configured.";
            }

            if (message.Contains("OPENAI_401", StringComparison.OrdinalIgnoreCase))
            {
                return "OpenAI API key is invalid or unauthorized.";
            }

            if (message.Contains("OPENAI_429", StringComparison.OrdinalIgnoreCase))
            {
                return "OpenAI quota or rate limit was exceeded.";
            }

            if (message.Contains("OPENAI_ERROR", StringComparison.OrdinalIgnoreCase))
            {
                return $"OpenAI request failed. {ExtractProviderDetails(message)}";
            }

            if (message.Contains("OLLAMA_CONNECTION", StringComparison.OrdinalIgnoreCase))
            {
                return "Ollama is not running on http://localhost:11434.";
            }

            if (message.Contains("OLLAMA_MODEL_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return $"{GetModeLabel(aiMode)} model is not installed in Ollama.";
            }

            if (message.Contains("OLLAMA_ERROR", StringComparison.OrdinalIgnoreCase))
            {
                return $"Ollama request failed. {ExtractProviderDetails(message)}";
            }

            return message;
        }

        private static string ExtractProviderDetails(string message)
        {
            var separatorIndex = message.IndexOf(':');

            if (separatorIndex >= 0 && separatorIndex < message.Length - 1)
            {
                return message[(separatorIndex + 1)..].Trim();
            }

            return message;
        }

        [HttpGet("ai-modes")]
        public IActionResult GetAiModes()
        {
            return Ok(new
            {
                defaultMode = "fast",
                modes = new[]
                {
            new
            {
                key = "fast",
                description = "Quick response using local AI model"
            },
            new
            {
                key = "balanced",
                description = "Balanced quality and speed"
            },
            new
            {
                key = "advanced",
                description = "Higher quality response using OpenAI"
            }
        }
            });
        }
    }
}
