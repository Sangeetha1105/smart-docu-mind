namespace SmartDocuMind.Models
{
    // Represents one chat message sent to AI provider
    public class Message
    {
        // Role can be system, user, or assistant
        public string Role { get; set; } = default!;

        // Actual text content of the message
        public string Content { get; set; } = default!;
    }
}