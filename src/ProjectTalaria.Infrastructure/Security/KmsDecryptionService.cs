using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using System.Security.Cryptography;

namespace ProjectTalaria.Infrastructure.Security;

public class KmsDecryptionService(IAmazonKeyManagementService kmsClient)
{

    public async Task<Stream> DecryptStreamAsync(Stream encryptedStream, byte[] encryptedKey)
    {
        byte[] plaintextKey;

        var decryptRequest = new DecryptRequest
        {
            CiphertextBlob = new MemoryStream(encryptedKey)
        };
        var decryptResponse = await kmsClient.DecryptAsync(decryptRequest);
        plaintextKey = decryptResponse.Plaintext.ToArray();

        using var aes = Aes.Create();
        aes.Key = plaintextKey;

        try
        {
            byte[] iv = new byte[16];
            await encryptedStream.ReadAsync(iv, 0, 16);
            encryptedStream.Position = 0;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var cryptoStream = new CryptoStream(encryptedStream, decryptor, CryptoStreamMode.Read, leaveOpen: true);

            Array.Clear(plaintextKey, 0, plaintextKey.Length);
            return cryptoStream;
        }
        catch
        {
            Array.Clear(plaintextKey, 0, plaintextKey.Length);
            throw;
        }
    }
}