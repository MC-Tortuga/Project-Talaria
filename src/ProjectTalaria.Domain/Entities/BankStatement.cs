namespace ProjectTalaria.Domain.Entities;
public class BankStatement {
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime StatementDate { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public byte[] EncryptedDataKey { get; set; } = Array.Empty<byte>();
}
