namespace SmartDocuMind.Models
{
    // Response returned after file upload
    public class UploadResult
    {
        // Generated unique file id
        public string FileId { get; set; } = default!;

        // Original uploaded file name
        public string FileName { get; set; } = default!;

        // Uploaded file size in bytes
        public long FileSize { get; set; }

        // Number of extracted lines
        public int Lines { get; set; }

        // Small preview of extracted content
        public string Preview { get; set; } = default!;
    }
}