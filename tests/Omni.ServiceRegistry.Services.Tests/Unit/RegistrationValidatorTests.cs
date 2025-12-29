using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Omni.ServiceRegistry.Services;
using Xunit;

namespace Omni.ServiceRegistry.Services.Tests.Unit;

/// <summary>
/// Unit tests for RegistrationValidator
/// </summary>
public class RegistrationValidatorTests
{
    private readonly RegistrationValidator validator;

    public RegistrationValidatorTests()
    {
        validator = new RegistrationValidator();
    }

    #region Service Name Validation Tests

    [Theory]
    [InlineData("ValidName")]
    [InlineData("valid-name")]
    [InlineData("Valid123")]
    [InlineData("123Valid")]
    [InlineData("abc")]
    public void ValidateServiceName_ValidName_ReturnsTrue(string serviceName)
    {
        // Act
        bool result = validator.ValidateServiceName(serviceName, out string? error);

        // Assert
        Assert.True(result);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("ab", "Service name must be 3-50 alphanumeric characters or hyphens")]
    [InlineData("", "Service name is required")]
    [InlineData("ThisIsAReallyLongServiceNameThatExceedsFiftyCharactersLimit", "Service name must be 3-50 alphanumeric characters or hyphens")]
    [InlineData("Invalid Name", "Service name must be 3-50 alphanumeric characters or hyphens")]
    [InlineData("Invalid_Name", "Service name must be 3-50 alphanumeric characters or hyphens")]
    [InlineData("Invalid.Name", "Service name must be 3-50 alphanumeric characters or hyphens")]
    [InlineData("Invalid@Name", "Service name must be 3-50 alphanumeric characters or hyphens")]
    public void ValidateServiceName_InvalidName_ReturnsFalseWithError(string serviceName, string expectedError)
    {
        // Act
        bool result = validator.ValidateServiceName(serviceName, out string? error);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Equal(expectedError, error);
    }

    #endregion

    #region Email Validation Tests

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.user@domain.org")]
    [InlineData("admin+tag@company.co.uk")]
    [InlineData("name@subdomain.domain.com")]
    public void ValidateEmail_ValidEmail_ReturnsTrue(string email)
    {
        // Act
        bool result = validator.ValidateEmail(email, out string? error);

        // Assert
        Assert.True(result);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("", "Contact email is required")]
    [InlineData("notanemail", "Invalid email format")]
    [InlineData("@domain.com", "Invalid email format")]
    [InlineData("user@", "Invalid email format")]
    [InlineData("user domain.com", "Invalid email format")]
    public void ValidateEmail_InvalidEmail_ReturnsFalseWithError(string email, string expectedError)
    {
        // Act
        bool result = validator.ValidateEmail(email, out string? error);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Equal(expectedError, error);
    }

    #endregion

    #region Endpoints Validation Tests

    [Fact]
    public void ValidateEndpoints_ValidHttpUrls_ReturnsTrue()
    {
        // Arrange
        string endpointsJson = "[\"http://localhost:8080\", \"https://example.com\"]";

        // Act
        bool result = validator.ValidateEndpoints(endpointsJson, out string? error);

        // Assert
        Assert.True(result);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidateEndpoints_EmptyList_ReturnsFalseWithError()
    {
        // Arrange
        string endpointsJson = "[]";

        // Act
        bool result = validator.ValidateEndpoints(endpointsJson, out string? error);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Equal("At least 1 endpoint is required", error);
    }

    [Fact]
    public void ValidateEndpoints_TooManyEndpoints_ReturnsFalseWithError()
    {
        // Arrange
        List<string> endpoints = new();
        for (int i = 0; i < 11; i++)
        {
            endpoints.Add($"http://example{i}.com");
        }
        string endpointsJson = System.Text.Json.JsonSerializer.Serialize(endpoints);

        // Act
        bool result = validator.ValidateEndpoints(endpointsJson, out string? error);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Equal("Maximum 10 endpoints allowed", error);
    }

    [Theory]
    [InlineData("[\"http://valid.com\", \"not-a-url\"]", "Invalid endpoint URL: not-a-url")]
    [InlineData("[\"http://valid.com\", \"ftp://example.com\"]", "Invalid endpoint URL: ftp://example.com")]
    [InlineData("[\"example.com\"]", "Invalid endpoint URL: example.com")]
    public void ValidateEndpoints_InvalidUrl_ReturnsFalseWithError(string endpointsJson, string expectedError)
    {
        // Act
        bool result = validator.ValidateEndpoints(endpointsJson, out string? error);

        // Assert
        Assert.False(result);
        Assert.NotEmpty(error);
        Assert.Equal(expectedError, error);
    }

    #endregion

    #region Heartbeat Configuration Validation Tests

    [Theory]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(300)]
    public void ValidateHeartbeatTimeout_ValidValue_ReturnsTrue(int timeout)
    {
        // Act
        bool result = validator.ValidateHeartbeatTimeout(timeout, out string? error);

        // Assert
        Assert.True(result);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(0, "Heartbeat timeout must be positive")]
    [InlineData(-1, "Heartbeat timeout must be positive")]
    [InlineData(5, "Recommended range: 15-300 seconds")]
    [InlineData(400, "Recommended range: 15-300 seconds")]
    public void ValidateHeartbeatTimeout_InvalidValue_ReturnsFalseWithWarning(int timeout, string expectedMessage)
    {
        // Act
        bool result = validator.ValidateHeartbeatTimeout(timeout, out string? error);

        // Assert
        if (timeout <= 0)
        {
            Assert.False(result);
        }
        Assert.NotEmpty(error);
        Assert.Equal(expectedMessage, error);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(20)]
    public void ValidateMaxMissedHeartbeats_ValidValue_ReturnsTrue(int maxMissed)
    {
        // Act
        bool result = validator.ValidateMaxMissedHeartbeats(maxMissed, out string? error);

        // Assert
        Assert.True(result);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(0, "Max missed heartbeats must be positive")]
    [InlineData(-1, "Max missed heartbeats must be positive")]
    [InlineData(1, "Recommended range: 3-20")]
    [InlineData(25, "Recommended range: 3-20")]
    public void ValidateMaxMissedHeartbeats_InvalidValue_ReturnsFalseWithWarning(int maxMissed, string expectedMessage)
    {
        // Act
        bool result = validator.ValidateMaxMissedHeartbeats(maxMissed, out string? error);

        // Assert
        if (maxMissed <= 0)
        {
            Assert.False(result);
        }
        Assert.NotEmpty(error);
        Assert.Equal(expectedMessage, error);
    }

    #endregion
}
