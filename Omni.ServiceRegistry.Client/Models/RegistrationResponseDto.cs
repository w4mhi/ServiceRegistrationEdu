using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Client.Models;

public class RegistrationResponseDto
{
    public Guid RegistrationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ServiceId { get; set; }
}
