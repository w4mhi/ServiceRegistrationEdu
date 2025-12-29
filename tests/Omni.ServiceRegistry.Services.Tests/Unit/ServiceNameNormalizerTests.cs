using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Services;
using Xunit;

namespace Omni.ServiceRegistry.Services.Tests.Unit;

/// <summary>
/// Unit tests for ServiceNameNormalizer
/// </summary>
public class ServiceNameNormalizerTests
{
    private readonly ServiceNameNormalizer normalizer;

    public ServiceNameNormalizerTests()
    {
        normalizer = new ServiceNameNormalizer();
    }

    [Fact]
    public void Normalize_ValidServiceName_ReturnsLowercaseHash()
    {
        // Arrange
        string serviceName = "TestService";

        // Act
        string result = normalizer.Normalize(serviceName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(64, result.Length); // SHA256 produces 64 hex characters
        Assert.Matches("^[A-F0-9]+$", result); // Hex string (uppercase)
    }

    [Fact]
    public void Normalize_SameNameDifferentCase_ProducesSameHash()
    {
        // Arrange
        string name1 = "MyService";
        string name2 = "myservice";
        string name3 = "MYSERVICE";

        // Act
        string hash1 = normalizer.Normalize(name1);
        string hash2 = normalizer.Normalize(name2);
        string hash3 = normalizer.Normalize(name3);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public void Normalize_DifferentNames_ProduceDifferentHashes()
    {
        // Arrange
        string name1 = "Service1";
        string name2 = "Service2";

        // Act
        string hash1 = normalizer.Normalize(name1);
        string hash2 = normalizer.Normalize(name2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Normalize_WithHyphens_HandlesCorrectly()
    {
        // Arrange
        string serviceName = "My-Test-Service";

        // Act
        string result = normalizer.Normalize(serviceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void Normalize_EmptyString_ReturnsHash()
    {
        // Arrange
        string serviceName = "";

        // Act
        string result = normalizer.Normalize(serviceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void Normalize_Consistency_SameInputProducesSameOutput()
    {
        // Arrange
        string serviceName = "ConsistentService";

        // Act
        string hash1 = normalizer.Normalize(serviceName);
        string hash2 = normalizer.Normalize(serviceName);
        string hash3 = normalizer.Normalize(serviceName);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public void Normalize_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        string serviceName = "Service-123_Test";

        // Act
        string result = normalizer.Normalize(serviceName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(64, result.Length);
    }
}
