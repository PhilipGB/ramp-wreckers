using System;
using System.Collections.Generic;
using System.Linq;

namespace RampWreckers;

public enum Medal
{
    None = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3,
}

/// <summary>One authored campaign objective. No generated objectives.</summary>
public sealed record WreckChallenge(
    string Name,
    string Goal,
    int StatueIndex,
    string[] RequiredParts,
    int MinimumBrokenParts,
    int Reward,
    int SilverScore,
    int GoldScore,
    float GoldSeconds,
    int RecommendedRampDegrees,
    int RecommendedVehicle,
    string Hint)
{
    public string StatueName => StatueCatalog.Names[StatueIndex];
    public string RecommendedVehicleName => VehicleCatalog.Names[RecommendedVehicle];
}

/// <summary>Vehicle display names shared with the HUD recommendation line.</summary>
public static class VehicleCatalog
{
    public static readonly string[] Names = { "Rally Car", "Pickup Truck", "Monster Truck" };
}

/// <summary>The authored statue part inventories. Challenge validation uses these.</summary>
public static class StatueCatalog
{
    public static readonly string[] Names = { "Garden Gnome", "Iron Knight", "Reactor Robot" };

    public static readonly string[][] PartIds =
    {
        new[] { "LeftLeg", "RightLeg", "Torso", "Head", "Beard", "Hat", "LeftArm", "RightArm" },
        new[] { "LeftLeg", "RightLeg", "Torso", "Head", "LeftArm", "RightArm", "Shield" },
        new[] { "LeftLeg", "RightLeg", "Torso", "Head", "LeftArm", "RightArm", "Core", "Armour" },
    };

    public static bool HasPart(int statueIndex, string partId) =>
        statueIndex >= 0 && statueIndex < PartIds.Length &&
        PartIds[statueIndex].Contains(partId, StringComparer.Ordinal);

    public static bool StatueCatalogHasAllParts() =>
        Names.Length == 3 && PartIds.Length == 3 &&
        PartIds.All(parts => parts.Length >= 7 &&
            parts.All(id => !string.IsNullOrWhiteSpace(id)) &&
            parts.Distinct(StringComparer.Ordinal).Count() == parts.Length);
}

/// <summary>Nine hand-authored challenges plus a safe Free Smash mode.</summary>
public static class ChallengeCatalog
{
    public static readonly WreckChallenge[] All =
    {
        new("First Crash", "Break any 2 gnome pieces", 0,
            Array.Empty<string>(), 2, 100, 240, 620, 24f,
            10, 1, "Hold W and launch straight into the legs or belly. The pickup truck is forgiving."),
        new("Hat Trick", "Knock the gnome's hat off", 0,
            new[] { "Hat" }, 1, 150, 300, 700, 26f,
            27, 0, "Use the suggested 27° ramp: the fast rally car flies highest and clips the hat."),
        new("Gnome Down", "Break 4 gnome pieces", 0,
            Array.Empty<string>(), 4, 200, 600, 1400, 28f,
            10, 1, "Aim low and smash both legs with the pickup to bring the gnome down. Retry resets it."),
        new("Shield Breaker", "Detach the knight's shield", 1,
            new[] { "Shield" }, 1, 200, 320, 760, 26f,
            18, 1, "The shield hangs on the knight's left arm, just left of your straight run. Launch the pickup at 18° without steering and it clips the shield."),
        new("Knee Trouble", "Break both knight legs", 1,
            new[] { "LeftLeg", "RightLeg" }, 2, 250, 700, 1500, 32f,
            10, 0, "Aim low at both knees with the rally car — it sits lowest. Retry resets the statue."),
        new("Knightfall", "Break 5 knight pieces", 1,
            Array.Empty<string>(), 5, 300, 1000, 2200, 34f,
            10, 1, "Smash both legs with the pickup and the armoured body comes down with them."),
        new("Peel the Armour", "Remove the robot's chest armour", 2,
            new[] { "Armour" }, 1, 250, 75, 85, 28f,
            20, 0, "The chest plate sits high: the rally car at 20° flies straight into it. Ground traffic only breaks the legs."),
        new("Core Shot", "Break the exposed reactor core", 2,
            new[] { "Core" }, 1, 350, 200, 260, 40f,
            20, 0, "Rally at 20°: the first strike peels the chest armour, then reverse and ram the exposed core. Retry resets the statue."),
        new("Robot Wreck", "Break 6 robot pieces", 2,
            Array.Empty<string>(), 6, 500, 1700, 3400, 44f,
            16, 2, "The monster truck's weight smashes the robot apart at 16°. Retry resets it."),
    };

    public static bool StatueCatalogHasAllParts() => StatueCatalog.StatueCatalogHasAllParts();

    public static bool IsComplete(WreckChallenge goal, IReadOnlySet<string> broken)
    {
        if (broken.Count < goal.MinimumBrokenParts)
            return false;
        foreach (string part in goal.RequiredParts)
            if (!broken.Contains(part))
                return false;
        return true;
    }

    public static Medal MedalFor(WreckChallenge goal, IReadOnlySet<string> broken,
        int impactScore, float runSeconds)
    {
        if (!IsComplete(goal, broken))
            return Medal.None;
        if (impactScore >= goal.GoldScore && runSeconds <= goal.GoldSeconds)
            return Medal.Gold;
        if (impactScore >= goal.SilverScore)
            return Medal.Silver;
        return Medal.Bronze;
    }

    /// <summary>Static validation shared by the unit tests and the runtime.</summary>
    public static bool TryValidate(out List<string> errors)
    {
        errors = new List<string>();
        if (All.Length != 9)
            errors.Add($"Expected 9 challenges, found {All.Length}.");
        for (int i = 0; i < All.Length; i++)
        {
            WreckChallenge challenge = All[i];
            if (challenge.StatueIndex < 0 || challenge.StatueIndex >= StatueCatalog.PartIds.Length)
            {
                errors.Add($"Challenge {i + 1} targets unknown statue {challenge.StatueIndex}.");
                continue;
            }
            if (challenge.RecommendedVehicle < 0 || challenge.RecommendedVehicle > 2)
                errors.Add($"Challenge {i + 1} ({challenge.Name}) recommends unknown vehicle {challenge.RecommendedVehicle}.");
            if (challenge.Reward <= 0)
                errors.Add($"Challenge {i + 1} ({challenge.Name}) has no reward.");
            if (challenge.MinimumBrokenParts <= 0 ||
                challenge.MinimumBrokenParts > StatueCatalog.PartIds[challenge.StatueIndex].Length)
                errors.Add($"Challenge {i + 1} ({challenge.Name}) has an impossible part count.");
            if (challenge.SilverScore < 0 || challenge.GoldScore < challenge.SilverScore)
                errors.Add($"Challenge {i + 1} ({challenge.Name}) has invalid medal scores.");
            if (challenge.GoldSeconds <= 0)
                errors.Add($"Challenge {i + 1} ({challenge.Name}) has an invalid gold time.");
            foreach (string part in challenge.RequiredParts)
                if (!StatueCatalog.HasPart(challenge.StatueIndex, part))
                    errors.Add($"Challenge {i + 1} ({challenge.Name}) requires missing part '{part}' on {StatueCatalog.Names[challenge.StatueIndex]}.");
        }
        return errors.Count == 0;
    }

    public static string MedalLabel(Medal medal) => medal switch
    {
        Medal.Gold => "GOLD",
        Medal.Silver => "SILVER",
        Medal.Bronze => "BRONZE",
        _ => "—",
    };
}
