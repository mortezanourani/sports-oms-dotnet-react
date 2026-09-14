namespace Backend.Models
{
    public class Message
    {
        public Guid Id { get; set; }
        public string? Subject { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid SenderId { get; set; }
        public ApplicationUser Sender { get; set; } = null!;

        public ICollection<MessageRecipient> Recipients { get; set; } = new List<MessageRecipient>();
        public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
    }
}
