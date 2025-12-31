using System;
using System.Collections.Generic;

namespace Omni.ServiceRegistry.Dashboard.DTOs;

public class BulkApprovalResponseDto
{
    public List<Guid> SuccessfulApprovals { get; set; } = new();
    public List<BulkApprovalFailureDto> Failures { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

public class BulkApprovalFailureDto
{
    public Guid RegistrationId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
