using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations;

namespace Omni.ServiceRegistry.Api.DTOs;

/// <summary>
/// Request DTO for denying a registration (R12-R13)
/// </summary>
public class DenialRequestDto
{
    /// <summary>
    /// Administrator performing the denial
    /// </summary>
    [Required]
    public string DeniedBy { get; set; } = string.Empty;
    
    /// <summary>
    /// Reason for denial (required per R13)
    /// </summary>
    [Required]
    [MinLength(10)]
    public string Comments { get; set; } = string.Empty;
}
