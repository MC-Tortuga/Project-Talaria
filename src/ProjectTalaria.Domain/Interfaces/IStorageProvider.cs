namespace ProjectTalaria.Domain.Interfaces;
public interface IStorageProvider {
    Task<Stream> GetFileStreamAsync(string blobName, CancellationToken ct = default);
}
