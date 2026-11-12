using System;
using System.Collections.Generic;
using System.Linq;

namespace RampWreckers;

/// <summary>
/// Pure objective-progress model shared by the HUD and tests. Named components are
/// reported separately from the total-part threshold so "broken 5 (goal 2)" can
/// never masquerade as knee progress.
/// </summary>
public readonly record struct ObjectiveProgress(
    string RequiredLabel,
    string TotalLabel,
    int RequiredBroken,
    int RequiredTotal,
    int TotalBroken,
    int MinimumTotal,
    bool RequiredComplete,
    bool TotalComplete,
    bool Complete)
{
    public string CombinedLabel => RequiredTotal > 0
        ? $"{RequiredLabel}   |   {TotalLabel}"
        : TotalLabel;
}

public static class ObjectiveProgressRules
{
    public static string ObjectiveLabel(WreckChallenge challenge) => challenge.Name switch
    {
        "Knee Trouble" => "BOTH KNEES",
        "Hat Trick" => "HAT",
        "Shield Breaker" => "SHIELD",
        "Core Shot" => "REACTOR CORE",
        "Peel the Armour" => "CHEST ARMOUR",
        _ => "TARGET PARTS",
    };

    public static ObjectiveProgress Evaluate(WreckChallenge challenge,
        IReadOnlySet<string> broken)
    {
        int requiredBroken = challenge.RequiredParts.Count(broken.Contains);
        int requiredTotal = challenge.RequiredParts.Length;
        bool requiredComplete = requiredTotal == 0 || requiredBroken == requiredTotal;
        bool totalComplete = broken.Count >= challenge.MinimumBrokenParts;
        string requiredLabel = requiredTotal > 0
            ? $"{ObjectiveLabel(challenge)}   {requiredBroken} / {requiredTotal}"
            : "";
        string totalLabel = $"TOTAL PARTS   {broken.Count} / {challenge.MinimumBrokenParts}";
        return new ObjectiveProgress(
            requiredLabel,
            totalLabel,
            requiredBroken,
            requiredTotal,
            broken.Count,
            challenge.MinimumBrokenParts,
            requiredComplete,
            totalComplete,
            requiredComplete && totalComplete);
    }

    public static string RequiredPartLabel(string partId) => partId switch
    {
        "LeftLeg" => "Left knee",
        "RightLeg" => "Right knee",
        "Hat" => "Hat",
        "Shield" => "Shield",
        "Core" => "Reactor core",
        "Armour" => "Chest armour",
        _ => partId,
    };
}
