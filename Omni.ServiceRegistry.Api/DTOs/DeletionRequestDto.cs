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
/// DTO for requesting service deletion
/// </summary>
public class DeletionRequestDto
{
    /// <summary>
    /// Reason for deletion request (required)
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional additional comments
    /// </summary>
    public string? Comments { get; set; }
}
