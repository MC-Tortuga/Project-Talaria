namespace ProjectTalaria.Domain.Entities;

public class AccessToken
{
    public Guid Id { get; set; }
    public string TokenValue { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public TokenStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

public enum TokenStatus
{
    Active,
    Used,
    Expired
}