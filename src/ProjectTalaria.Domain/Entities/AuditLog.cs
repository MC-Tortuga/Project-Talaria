namespace ProjectTalaria.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    public AuditAction Action { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}

public enum AuditAction
{
    TokenGenerated,
    TokenBurned,
    DownloadRequested,
    DownloadCompleted,
    AccessDenied,
    InvalidToken
}