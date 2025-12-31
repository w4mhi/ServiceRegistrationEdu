namespace Omni.ServiceRegistry.Api.DTOs;

/// <summary>
/// Response DTO for restoration eligibility check
/// </summary>
public class RestorationEligibilityDto
{
    /// <summary>
    /// Whether the service is eligible for restoration
    /// </summary>
    public bool IsEligible { get; set; }
    
    /// <summary>
    /// Reason for eligibility or ineligibility
    /// </summary>
    public string? Reason { get; set; }
    
    /// <summary>
    /// Type of restoration available: "Quick" or "Full"
    /// </summary>
    public string? RestorationType { get; set; }
    
    /// <summary>
    /// Days remaining in quick restore window (null if not applicable)
    /// </summary>
    public int? DaysUntilExpiration { get; set; }
}
