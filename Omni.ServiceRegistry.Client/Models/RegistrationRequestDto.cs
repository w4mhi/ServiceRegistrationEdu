using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Omni.ServiceRegistry.Models;

namespace Omni.ServiceRegistry.Client.Models;

public class RegistrationRequestDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public List<string> Endpoints { get; set; } = new();
    public List<ApiEndpoint>? ApiEndpoints { get; set; }
    public int HeartbeatTimeout { get; set; }
    public int MaxMissedHeartbeats { get; set; }
}
