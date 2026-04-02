using SmartDocuMind.Models;

namespace SmartDocuMind.Services
{
    public interface IAIService
    {
        string ProviderName { get; }

        // Return the full assistant answer after streaming
        Task<string> StreamAnswerAsync(
            string model,
            List<Message> messages,
            HttpResponse response);
    }
}