namespace Backend.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool Revoked { get; set; } = false;

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;
    }
}
