namespace Backend.Models
{
    public class MessageRecipient
    {
        public Guid Id { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        public Guid MessageId { get; set; }
        public Message Message { get; set; } = null!;

        public Guid RecipientId { get; set; }
        public ApplicationUser Recipient { get; set; } = null!;
    }
}
