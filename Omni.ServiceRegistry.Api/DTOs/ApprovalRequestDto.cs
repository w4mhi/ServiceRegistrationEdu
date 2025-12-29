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
