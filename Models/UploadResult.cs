namespace SmartDocuMind.Models
{
    public class UploadResult
    {
        public string FileId { get; set; } = default!;
        public string FileName { get; set; } = default!;
        public long FileSize { get; set; }
        public int Lines { get; set; }
        public string Preview { get; set; } = default!;
    }
}