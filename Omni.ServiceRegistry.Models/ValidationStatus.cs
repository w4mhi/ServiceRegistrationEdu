namespace Omni.ServiceRegistry.Models;

/// <summary>
/// Contract validation status for registration requests
/// </summary>
public enum ValidationStatus
{
    /// <summary>
    /// Not yet validated
    /// </summary>
    NotValidated = 0,

    /// <summary>
    /// Validation passed - service meets requirements
    /// </summary>
    Passed = 1,

    /// <summary>
    /// Validation failed - service does not meet requirements
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Validation in progress
    /// </summary>
    InProgress = 3
}
