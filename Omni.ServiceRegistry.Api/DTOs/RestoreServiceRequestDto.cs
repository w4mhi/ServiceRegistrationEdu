namespace Omni.ServiceRegistry.Api.DTOs;

/// <summary>
/// Request DTO for restoring a service
/// </summary>
public class RestoreServiceRequestDto
{
    /// <summary>
    /// Administrator who is performing the restoration
    /// </summary>
    public string RestoredBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Reason for restoring the service (minimum 10 characters)
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed justification for restoration (required for Full Restore, minimum 50 characters)
    /// </summary>
    public string? Justification { get; set; }
    
    /// <summary>
    /// Confirmation that service owner authorization has been verified (required for Full Restore)
    /// </summary>
    public bool OwnerVerified { get; set; }
    
    /// <summary>
    /// Confirmation that service endpoints are still valid (required for Full Restore)
    /// </summary>
    public bool EndpointsVerified { get; set; }
}
