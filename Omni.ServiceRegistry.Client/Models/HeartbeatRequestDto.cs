using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Omni.ServiceRegistry.Client.Models;

public class HeartbeatRequestDto
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
