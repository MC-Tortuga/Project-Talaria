using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ProjectTalaria.ControlPlane.Api.Services;
using ProjectTalaria.Domain.Entities;
using ProjectTalaria.Domain.Interfaces;
using ProjectTalaria.Infrastructure.CDN;
using Xunit;

namespace ProjectTalaria.Tests.Unit;

public class TokenGeneratorTests
{
    private readonly Mock<IAccessTokenRepository> _tokenRepositoryMock;
    private readonly Mock<ICdnService> _cdnServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly TokenGenerator _tokenGenerator;

    public TokenGeneratorTests()
    {
        _tokenRepositoryMock = new Mock<IAccessTokenRepository>();
        _cdnServiceMock = new Mock<ICdnService>();
        _configurationMock = new Mock<IConfiguration>();

        _configurationMock.Setup(c => c["TokenSettings:ByteSize"]).Returns("32");
        _configurationMock.Setup(c => c["TokenSettings:ExpiryMinutes"]).Returns("5");

        _cdnServiceMock.Setup(c => c.IsEnabled).Returns(false);

        _tokenGenerator = new TokenGenerator(
            _tokenRepositoryMock.Object,
            _cdnServiceMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldCreateTokenWithCorrectProperties()
    {
        var documentId = Guid.NewGuid();
        var userId = "test-user-123";
        var streamBaseUrl = "http://localhost:5001";

        _tokenRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<AccessToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessToken t, CancellationToken _) => t);

        var (plaintextToken, streamUrl) = await _tokenGenerator.GenerateAccessTokenAsync(
            documentId, userId, streamBaseUrl);

        plaintextToken.Should().NotBeNullOrEmpty();
        plaintextToken.Length.Should().BeGreaterThan(20);
        streamUrl.Should().Contain(documentId.ToString());
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldStoreHashedTokenInRepository()
    {
        var documentId = Guid.NewGuid();
        var userId = "test-user-123";
        var streamBaseUrl = "http://localhost:5001";

        AccessToken? storedToken = null;
        _tokenRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<AccessToken>(), It.IsAny<CancellationToken>()))
            .Callback<AccessToken, CancellationToken>((t, _) => storedToken = t)
            .ReturnsAsync((AccessToken t, CancellationToken _) => t);

        await _tokenGenerator.GenerateAccessTokenAsync(documentId, userId, streamBaseUrl);

        storedToken.Should().NotBeNull();
        storedToken!.UserId.Should().Be(userId);
        storedToken.DocumentId.Should().Be(documentId);
        storedToken.Status.Should().Be(TokenStatus.Active);
        storedToken.TokenValue.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldReturnStreamUrlWithToken()
    {
        var documentId = Guid.NewGuid();
        var userId = "test-user-123";
        var streamBaseUrl = "http://localhost:5001";

        _tokenRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<AccessToken>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessToken t, CancellationToken _) => t);

        var (_, streamUrl) = await _tokenGenerator.GenerateAccessTokenAsync(
            documentId, userId, streamBaseUrl);

        streamUrl.Should().Be($"{streamBaseUrl}/stream/{documentId}");
    }
}