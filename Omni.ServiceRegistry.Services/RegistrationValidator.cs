using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Text.Json;
using System.Text.RegularExpressions;
using Omni.ServiceRegistry.Interfaces;

namespace Omni.ServiceRegistry.Services;

/// <summary>
/// Validation service for registration requests (R6)
/// </summary>
public partial class RegistrationValidator : IRegistrationValidator
{
    private const int ServiceNameMinLength = 3;
    private const int ServiceNameMaxLength = 50;
    private const int MinEndpoints = 1;
    private const int MaxEndpoints = 10;
    private const int RecommendedMinTimeout = 15;
    private const int RecommendedMaxTimeout = 300;
    private const int RecommendedMinMissed = 3;
    private const int RecommendedMaxMissed = 20;

    [GeneratedRegex(@"^[a-zA-Z0-9-]{3,50}$")]
    private static partial Regex ServiceNameRegex();

    [GeneratedRegex(@"^[^@]+@[^@]+\.[^@]+$")]
    private static partial Regex EmailRegex();

    public bool ValidateServiceName(string serviceName, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            errorMessage = "Service name is required";
            return false;
        }

        if (!ServiceNameRegex().IsMatch(serviceName))
        {
            errorMessage = $"Service name must be {ServiceNameMinLength}-{ServiceNameMaxLength} alphanumeric characters or hyphens";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public bool ValidateEmail(string email, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            errorMessage = "Contact email is required";
            return false;
        }

        if (!EmailRegex().IsMatch(email))
        {
            errorMessage = "Invalid email format";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public bool ValidateEndpoints(string endpointsJson, out string errorMessage)
    {
        try
        {
            List<string>? endpoints = JsonSerializer.Deserialize<List<string>>(endpointsJson);
            
            if (endpoints == null || endpoints.Count < MinEndpoints)
            {
                errorMessage = $"At least {MinEndpoints} endpoint is required";
                return false;
            }

            if (endpoints.Count > MaxEndpoints)
            {
                errorMessage = $"Maximum {MaxEndpoints} endpoints allowed";
                return false;
            }

            foreach (string endpoint in endpoints)
            {
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    errorMessage = $"Invalid endpoint URL: {endpoint}";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            errorMessage = "Endpoints must be a valid JSON array of URLs";
            return false;
        }
    }

    public bool ValidateHeartbeatTimeout(int timeout, out string errorMessage)
    {
        if (timeout <= 0)
        {
            errorMessage = "Heartbeat timeout must be positive";
            return false;
        }

        if (timeout < RecommendedMinTimeout || timeout > RecommendedMaxTimeout)
        {
            errorMessage = $"Recommended range: {RecommendedMinTimeout}-{RecommendedMaxTimeout} seconds";
        }
        else
        {
            errorMessage = string.Empty;
        }

        return true;
    }

    public bool ValidateMaxMissedHeartbeats(int maxMissed, out string errorMessage)
    {
        if (maxMissed <= 0)
        {
            errorMessage = "Max missed heartbeats must be positive";
            return false;
        }

        if (maxMissed < RecommendedMinMissed || maxMissed > RecommendedMaxMissed)
        {
            errorMessage = $"Recommended range: {RecommendedMinMissed}-{RecommendedMaxMissed}";
        }
        else
        {
            errorMessage = string.Empty;
        }

        return true;
    }
}
