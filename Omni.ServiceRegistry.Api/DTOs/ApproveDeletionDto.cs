using System;

namespace Omni.ServiceRegistry.Api.DTOs;

/// <summary>
/// Request DTO for approving a service deletion
/// </summary>
public class ApproveDeletionDto
{
    /// <summary>
    /// Admin's reason for approving the deletion
    /// </summary>
    public string? Reason { get; set; }
}
