using System;
using System.Timers;

namespace Omni.ServiceRegistry.Dashboard.Services;

/// <summary>
/// Client-side service to track 60-second cooldown between analysis requests
/// </summary>
public class CooldownTimerService : IDisposable
{
    private DateTime? lastTriggerTime;
    private System.Timers.Timer? countdownTimer;
    private int remainingSeconds;

    public event Action? OnCooldownChanged;

    public int RemainingSeconds => remainingSeconds;
    public bool IsOnCooldown => remainingSeconds > 0;

    public CooldownTimerService()
    {
        remainingSeconds = 0;
    }

    /// <summary>
    /// Start cooldown timer
    /// </summary>
    public void StartCooldown()
    {
        lastTriggerTime = DateTime.UtcNow;
        remainingSeconds = 60;

        countdownTimer?.Stop();
        countdownTimer?.Dispose();

        countdownTimer = new System.Timers.Timer(1000);
        countdownTimer.Elapsed += OnTimerElapsed;
        countdownTimer.AutoReset = true;
        countdownTimer.Start();

        OnCooldownChanged?.Invoke();
    }

    /// <summary>
    /// Reset cooldown (for error scenarios)
    /// </summary>
    public void ResetCooldown()
    {
        remainingSeconds = 0;
        lastTriggerTime = null;
        countdownTimer?.Stop();
        OnCooldownChanged?.Invoke();
    }

    /// <summary>
    /// Check if enough time has passed since last trigger
    /// </summary>
    public bool CanTrigger()
    {
        if (lastTriggerTime == null)
        {
            return true;
        }

        TimeSpan elapsed = DateTime.UtcNow - lastTriggerTime.Value;
        return elapsed.TotalSeconds >= 60;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (lastTriggerTime == null)
        {
            remainingSeconds = 0;
            countdownTimer?.Stop();
            OnCooldownChanged?.Invoke();
            return;
        }

        TimeSpan elapsed = DateTime.UtcNow - lastTriggerTime.Value;
        remainingSeconds = Math.Max(0, 60 - (int)elapsed.TotalSeconds);

        if (remainingSeconds == 0)
        {
            countdownTimer?.Stop();
        }

        OnCooldownChanged?.Invoke();
    }

    public void Dispose()
    {
        countdownTimer?.Stop();
        countdownTimer?.Dispose();
    }
}
