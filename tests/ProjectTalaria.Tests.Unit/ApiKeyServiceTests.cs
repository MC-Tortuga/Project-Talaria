using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using ProjectTalaria.ControlPlane.Api.Services;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Infrastructure.Data;
using Xunit;

namespace ProjectTalaria.Tests.Unit;

public class ApiKeyServiceTests : IDisposable
{
    private readonly TalariaDbContext _dbContext;
    private readonly ApiKeyService _apiKeyService;

    public ApiKeyServiceTests()
    {
        var options = new DbContextOptionsBuilder<TalariaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TalariaDbContext(options);
        _apiKeyService = new ApiKeyService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateApiKeyAsync_ShouldGeneratePlainKeyAndHashedKey()
    {
        var userId = Guid.NewGuid();
        var name = "Test API Key";

        var (plainKey, storedKey) = await _apiKeyService.CreateApiKeyAsync(userId, name);

        plainKey.Should().NotBeNullOrEmpty();
        storedKey.KeyHash.Should().NotBeEmpty();
        plainKey.Should().NotBe(storedKey.KeyHash);
    }

    [Fact]
    public async Task CreateApiKeyAsync_ShouldStoreKeyWithCorrectProperties()
    {
        var userId = Guid.NewGuid();
        var name = "Test API Key";

        var (plainKey, storedKey) = await _apiKeyService.CreateApiKeyAsync(userId, name);

        storedKey.UserId.Should().Be(userId);
        storedKey.Name.Should().Be(name);
        storedKey.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ShouldReturnTrueForValidKey()
    {
        var userId = Guid.NewGuid();
        var name = "Test Key";
        var (plainKey, _) = await _apiKeyService.CreateApiKeyAsync(userId, name);

        var isValid = await _apiKeyService.ValidateApiKeyAsync(plainKey);

        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_ShouldReturnFalseForInvalidKey()
    {
        var isValid = await _apiKeyService.ValidateApiKeyAsync("invalid-key");

        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_ShouldReturnTrueWhenKeyOwnedByUser()
    {
        var userId = Guid.NewGuid();
        var name = "Test Key";
        var (_, storedKey) = await _apiKeyService.CreateApiKeyAsync(userId, name);

        var result = await _apiKeyService.RevokeApiKeyAsync(storedKey.Id, userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_ShouldReturnFalseWhenKeyNotOwned()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var name = "Test Key";
        var (_, storedKey) = await _apiKeyService.CreateApiKeyAsync(userId, name);

        var result = await _apiKeyService.RevokeApiKeyAsync(storedKey.Id, otherUserId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserIdFromKeyAsync_ShouldReturnUserIdForValidKey()
    {
        var userId = Guid.NewGuid();
        var name = "Test Key";
        var (plainKey, _) = await _apiKeyService.CreateApiKeyAsync(userId, name);

        var result = await _apiKeyService.GetUserIdFromKeyAsync(plainKey);

        result.Should().Be(userId);
    }
}