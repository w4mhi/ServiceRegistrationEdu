using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Api.DTOs;

/// <summary>
/// Request DTO for approving a registration (R11)
/// </summary>
public class ApprovalRequestDto
{
    /// <summary>
    /// Administrator performing the approval
    /// </summary>
    public string ApprovedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional comments
    /// </summary>
    public string? Comments { get; set; }
}

/// <summary>
/// Request DTO for bulk approving multiple registrations
/// </summary>
public class BulkApprovalRequestDto
{
    /// <summary>
    /// List of registration IDs to approve
    /// </summary>
    public List<Guid> RegistrationIds { get; set; } = new();
    
    /// <summary>
    /// Administrator performing the approvals
    /// </summary>
    public string ApprovedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Global comment applied to all approvals
    /// </summary>
    public string? Comments { get; set; }
}

/// <summary>
/// Response DTO for bulk approval operation
/// </summary>
public class BulkApprovalResponseDto
{
    /// <summary>
    /// Successfully approved registration IDs
    /// </summary>
    public List<Guid> SuccessfulApprovals { get; set; } = new();
    
    /// <summary>
    /// Failed approvals with error messages
    /// </summary>
    public List<BulkApprovalFailureDto> Failures { get; set; } = new();
    
    /// <summary>
    /// Total number of registrations processed
    /// </summary>
    public int TotalProcessed { get; set; }
    
    /// <summary>
    /// Number of successful approvals
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Number of failed approvals
    /// </summary>
    public int FailureCount { get; set; }
}

/// <summary>
/// Information about a failed approval in bulk operation
/// </summary>
public class BulkApprovalFailureDto
{
    /// <summary>
    /// Registration ID that failed to approve
    /// </summary>
    public Guid RegistrationId { get; set; }
    
    /// <summary>
    /// Error message explaining the failure
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
