using Microsoft.Extensions.Configuration;
using ProjectTalaria.Domain.Interfaces;

namespace ProjectTalaria.Infrastructure.Storage;

public class LocalFileStorageProvider(IConfiguration config) : IStorageProvider
{
    private readonly string _basePath = config["Storage:LocalPath"] ?? "statements";

    public async Task<Stream> GetFileStreamAsync(string blobName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, blobName);
        
        Console.WriteLine($"[DEBUG] LocalFileStorageProvider: basePath={_basePath}, blobName={blobName}, fullPath={filePath}");
        Console.WriteLine($"[DEBUG] File exists: {File.Exists(filePath)}");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Statement file not found: {blobName} (full path: {filePath})");
        }

        var memStream = new MemoryStream();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await fileStream.CopyToAsync(memStream, ct);
        memStream.Position = 0;
        return memStream;
    }
}
