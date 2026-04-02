using System.ComponentModel.DataAnnotations;

namespace SmartDocuMind.Models
{
    public class QuestionRequest
    {
        [Required]
        public string FileId { get; set; } = default!;

        [Required]
        public string Question { get; set; } = default!;

        /// <summary>
        /// Select AI mode:
        /// Fast = quick response,
        /// Balanced = better quality,
        /// Advanced = highest quality (OpenAI)
        /// </summary>
        public AiMode? AiMode { get; set; }
    }
}