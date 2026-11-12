using System;

namespace RampWreckers;

/// <summary>
/// Deterministic impact rules. This is deliberately pure .NET so the scorer can be
/// unit-tested without a physics engine and tuned without touching scene code.
/// </summary>
public static class ImpactModel
{
    /// <summary>Below this relative speed a nudge is treated as contact, not damage.</summary>
    public const float MinimumImpactSpeed = 4.5f;

    /// <summary>Energy units shown in the HUD and compared with part thresholds.</summary>
    public static float Energy(float massKg, float relativeSpeed, float weakPointMultiplier = 1f)
    {
        if (massKg <= 0f || relativeSpeed <= 0f)
            return 0f;
        return 0.5f * (massKg / 1000f) * relativeSpeed * relativeSpeed *
               MathF.Max(0.01f, weakPointMultiplier);
    }

    public static bool Breaks(float energy, float threshold) =>
        threshold > 0f && energy + 0.001f >= threshold;

    /// <summary>Impact score awarded for a destroyed component.</summary>
    public static int Score(float energy, int scoreValue, bool weakPoint)
    {
        float scaled = scoreValue * (0.65f + MathF.Min(energy, 1200f) / 1000f);
        if (weakPoint)
            scaled *= 1.6f;
        return Math.Max(1, (int)MathF.Round(scaled));
    }

    /// <summary>Collision energy transferred to an armour plate shielding a weak point.</summary>
    public static float ShieldTransfer(float energy) => energy * 0.5f;

    /// <summary>Recoil fraction after a protected hit. Keeps the vehicle from sticking.</summary>
    public static float ProtectedBounce(float energy) => Math.Clamp(energy / 500f, 0.2f, 1.8f);

    public static float ClampDebrisSpeed(float speed, float max = 34f) =>
        Math.Clamp(speed, 0f, max);

    /// <summary>Physics launch estimate used by the ramp-angle HUD hint.</summary>
    public static float EstimatedLaunchHeight(float rampDegrees, float speed, float gravity)
    {
        float radians = rampDegrees * MathF.PI / 180f;
        float vertical = MathF.Max(0f, speed * MathF.Sin(radians));
        return vertical * vertical / (2f * MathF.Max(1f, gravity));
    }
}
