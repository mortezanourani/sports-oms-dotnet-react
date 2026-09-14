namespace Backend.Models
{
    public class MessageAttachment
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public Guid MessageId { get; set; }
        public Message Message { get; set; } = null!;
    }
}
