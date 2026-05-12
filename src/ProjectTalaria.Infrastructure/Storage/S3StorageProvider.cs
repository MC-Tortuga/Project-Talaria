using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using ProjectTalaria.Domain.Interfaces;

namespace ProjectTalaria.Infrastructure.Storage;

public class S3StorageProvider(IAmazonS3 s3Client, IConfiguration config) : IStorageProvider
{
    private readonly string _bucketName = config["AWS:BucketName"] ?? "talaria-statements";

    public async Task<Stream> GetFileStreamAsync(string blobName, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = blobName
            };

            var response = await s3Client.GetObjectAsync(request, ct);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex)
        {
            throw new Exception($"Error fetching from S3: {ex.Message}");
        }
    }
}
