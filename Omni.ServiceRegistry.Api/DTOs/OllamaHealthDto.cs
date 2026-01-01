namespace Omni.ServiceRegistry.Api.DTOs;

public class OllamaHealthDto
{
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = string.Empty;
}
