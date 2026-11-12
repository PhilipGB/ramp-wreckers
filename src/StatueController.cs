using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RampWreckers;

/// <summary>
/// Authored structural destruction. Each part carries its own support/protection
/// links in the scene; this class only applies the shared, deterministic rules.
/// </summary>
public partial class StatueController : Node3D
{
    [Export] public int RequiredBrokenParts { get; set; } = 4;
    [Export] public string DisplayName { get; set; } = "Statue";
    [Export] public float LeanDegrees { get; set; } = 3.2f;

    public event Action<string, int, bool>? PartBroken;
    public event Action<string, string>? PartBlocked;
    public event Action<string, float>? PartResisted;
    public event Action? Collapsed;
    public event Action<string>? PartLostSupport;
    public event Action? Leaned;

    private readonly Dictionary<string, StatuePart> _parts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _broken = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loose = new(StringComparer.Ordinal);
    private readonly List<StatuePart> _debris = new();
    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
    private bool _collapsed;
    private float _lean;
    private bool _graphValidated;

    public IReadOnlySet<string> BrokenPartIds => _broken;
    public IReadOnlySet<string> LoosePartIds => _loose;
    public int BrokenCount => _broken.Count;
    public int PartCount => _parts.Count;
    public bool IsCollapsed => _collapsed;
    public IReadOnlyDictionary<string, StatuePart> Parts => _parts;

    public StatuePart? GetPart(string partId) =>
        _parts.TryGetValue(partId, out StatuePart? part) ? part : null;

    public override void _Ready()
    {
        CollectParts(this);
        ValidateGraph();

        if (_parts.Count == 0)
        {
            GD.PushError($"{Name}: no StatuePart children were authored.");
            return;
        }

        foreach (StatuePart part in _parts.Values)
        {
            part.CaptureInitialTransform();
            part.ResetPart();
        }
        GD.Print($"[Statue] {DisplayName}: {_parts.Count} authored parts, graph valid={_graphValidated}.");
    }

    private void CollectParts(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is StatuePart part)
            {
                if (string.IsNullOrWhiteSpace(part.PartId))
                {
                    GD.PushError($"{Name}: a StatuePart has no PartId.");
                    continue;
                }
                if (!_parts.TryAdd(part.PartId, part))
                    GD.PushError($"{Name}: duplicate statue part id '{part.PartId}'.");
            }
            CollectParts(child);
        }
    }

    private void ValidateGraph()
    {
        bool valid = true;
        foreach (StatuePart part in _parts.Values)
        {
            foreach (string support in part.SupportedBy)
            {
                if (!_parts.ContainsKey(support))
                {
                    GD.PushError($"{Name}/{part.PartId}: supported by missing part '{support}'.");
                    valid = false;
                }
            }
            foreach (string protector in part.ProtectedBy)
            {
                if (!_parts.ContainsKey(protector))
                {
                    GD.PushError($"{Name}/{part.PartId}: protected by missing part '{protector}'.");
                    valid = false;
                }
            }
            if (part.BreakThreshold <= 0f)
            {
                GD.PushError($"{Name}/{part.PartId}: break threshold must be positive.");
                valid = false;
            }
        }
        _graphValidated = valid;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Keep released pieces bounded; they are allowed to bounce and settle, but
        // never to tunnel away or gain unbounded energy.
        for (int i = _debris.Count - 1; i >= 0; i--)
        {
            StatuePart part = _debris[i];
            if (!GodotObject.IsInstanceValid(part))
            {
                _debris.RemoveAt(i);
                continue;
            }
            if (part.Freeze)
                continue;

            float speed = part.LinearVelocity.Length();
            if (speed > 34f)
                part.LinearVelocity = part.LinearVelocity / speed * 34f;
            float spin = part.AngularVelocity.Length();
            if (spin > 14f)
                part.AngularVelocity = part.AngularVelocity / spin * 14f;

            if (part.GlobalPosition.Y < -5f)
            {
                part.Freeze = true;
                part.Visible = false;
            }
        }
    }

    public void ScheduleImpact(StatuePart part, float energy, float speed,
        Vector3 direction, Vector3 hitPoint)
    {
        if (!_parts.ContainsKey(part.PartId) || _pending.Contains(part.PartId))
            return;
        _pending.Add(part.PartId);
        Callable.From(() =>
        {
            _pending.Remove(part.PartId);
            ResolveImpact(part.PartId, energy, speed, direction, hitPoint);
        }).CallDeferred();
    }

    /// <summary>Public deterministic entry point used by gameplay and tests.</summary>
    public ImpactResult ResolveImpact(string partId, float energy, float speed,
        Vector3 direction, Vector3 hitPoint = default)
    {
        if (!_parts.TryGetValue(partId, out StatuePart? part))
            return ImpactResult.Ignored;
        if (part.IsBroken)
            return ImpactResult.Ignored;
        if (speed < ImpactModel.MinimumImpactSpeed)
            return ImpactResult.Ignored;
        if (hitPoint == default)
            hitPoint = part.GlobalPosition;

        if (IsProtected(part, out StatuePart? protector))
        {
            PartBlocked?.Invoke(part.PartId, protector.PartId);
            // The protector takes the hit, but only the transferred fraction. This
            // can peel armour with a direct core strike without immediately
            // destroying the protected weak point.
            float transfer = ImpactModel.ShieldTransfer(energy);
            if (!protector.IsBroken && ImpactModel.Breaks(transfer, protector.BreakThreshold))
                BreakPart(protector, transfer, direction, hitPoint, false, "shield");
            return ImpactResult.Blocked;
        }

        if (!ImpactModel.Breaks(energy, part.BreakThreshold))
        {
            PartResisted?.Invoke(part.PartId, energy);
            return ImpactResult.Resisted;
        }

        bool weakPoint = part.WeakPointMultiplier > 1.01f;
        BreakPart(part, energy, direction, hitPoint, weakPoint, "direct");
        CascadeUnsupported(part, energy * 0.55f, direction, hitPoint);
        UpdateLean();

        if (!_collapsed && (_broken.Contains("Torso") || _broken.Count >= RequiredBrokenParts))
        {
            _collapsed = true;
            Collapsed?.Invoke();
            return ImpactResult.Collapsed;
        }
        return ImpactResult.Broken;
    }

    private bool IsProtected(StatuePart part, out StatuePart protector)
    {
        protector = null!;
        foreach (string protectorId in part.ProtectedBy)
        {
            if (_parts.TryGetValue(protectorId, out StatuePart? candidate) &&
                !candidate.IsBroken && !candidate.IsLoose)
            {
                protector = candidate;
                return true;
            }
        }
        return false;
    }

    private void BreakPart(StatuePart part, float energy, Vector3 direction,
        Vector3 hitPoint, bool weakPoint, string reason)
    {
        if (part.IsBroken)
            return;

        Vector3 away = part.GlobalPosition - GlobalPosition;
        away.Y = 0f;
        if (away.LengthSquared() > 0.001f)
            away = away.Normalized();

        float strength = Mathf.Clamp(energy / 80f, 2.0f, 8.5f);
        if (weakPoint)
            strength *= 1.25f;
        Vector3 push = (direction * 0.8f + Vector3.Up * 0.38f + away * 0.22f).Normalized();
        // Deterministic scatter based on the part id, so repeated runs stay fair.
        int hash = StringComparer.Ordinal.GetHashCode(part.PartId) & 0x7fffffff;
        float spread = ((hash % 100) / 100f - 0.5f) * 0.32f;
        push += GlobalTransform.Basis.X * spread;
        Vector3 impulse = push.Normalized() * strength;
        Vector3 torque = new(
            ((hash >> 3) % 100 / 100f - 0.5f) * 2.2f,
            ((hash >> 7) % 100 / 100f - 0.5f) * 2.2f,
            ((hash >> 11) % 100 / 100f - 0.5f) * 2.2f);

        part.Release(impulse, torque, loose: false);
        _broken.Add(part.PartId);
        _loose.Remove(part.PartId);
        if (!_debris.Contains(part))
            _debris.Add(part);
        int score = ImpactModel.Score(energy, part.ScoreValue, weakPoint);
        GD.Print($"[Destruction] {DisplayName}/{part.PartId} broke via {reason} " +
                 $"energy={energy:0} score={score} weak={weakPoint}.");
        PartBroken?.Invoke(part.PartId, score, weakPoint);
    }

    private void LoosenPart(StatuePart part, Vector3 direction, Vector3 hitPoint)
    {
        if (part.IsBroken || part.IsLoose)
            return;
        Vector3 away = part.GlobalPosition - GlobalPosition;
        away.Y = 0f;
        if (away.LengthSquared() > 0.001f)
            away = away.Normalized();
        Vector3 push = (direction * 0.35f + Vector3.Down * 0.6f + away * 0.15f).Normalized();
        part.Release(push * 1.2f, new Vector3(0.2f, 0.1f, -0.3f), loose: true);
        _loose.Add(part.PartId);
        _debris.Add(part);
        PartLostSupport?.Invoke(part.PartId);
        GD.Print($"[Destruction] {DisplayName}/{part.PartId} detached intact (support lost).");
    }

    /// <summary>Releases every component whose complete support set has failed.</summary>
    private void CascadeUnsupported(StatuePart source, float energy, Vector3 direction,
        Vector3 hitPoint)
    {
        bool changed = true;
        int guard = 0;
        while (changed && guard++ < 16)
        {
            changed = false;
            foreach (StatuePart candidate in _parts.Values.ToArray())
            {
                if (candidate.IsBroken || candidate.IsLoose || candidate.SupportedBy.Length == 0)
                    continue;
                bool allSupportGone = candidate.SupportedBy.All(id =>
                    _broken.Contains(id) || _loose.Contains(id));
                if (!allSupportGone)
                    continue;

                changed = true;
                if (candidate.CountsAsBrokenWhenUnsupported)
                {
                    BreakPart(candidate, energy, direction, hitPoint, false, "collapse");
                }
                else
                {
                    LoosenPart(candidate, direction, hitPoint);
                }
            }
        }
    }

    private void UpdateLean()
    {
        bool leftGone = _broken.Contains("LeftLeg") || _loose.Contains("LeftLeg");
        bool rightGone = _broken.Contains("RightLeg") || _loose.Contains("RightLeg");
        float target = 0f;
        if (leftGone != rightGone)
            target = leftGone ? LeanDegrees : -LeanDegrees;

        if (Mathf.Abs(target - _lean) < 0.01f)
            return;
        _lean = target;

        var pivot = new Vector3(0, 1.1f, 0);
        Basis rotation = new(Vector3.Back, Mathf.DegToRad(_lean));
        foreach (StatuePart part in _parts.Values)
        {
            if (part.IsSupport || part.IsBroken || part.IsLoose)
                continue;
            Transform3D initial = part.InitialLocalTransform;
            Vector3 offset = initial.Origin - pivot;
            Transform3D posed = new(rotation * initial.Basis, pivot + rotation * offset);
            part.SetPosedTransform(posed);
        }
        if (Mathf.Abs(_lean) > 0.01f)
            Leaned?.Invoke();
    }

    /// <summary>Restores every authored part to its exact initial pose and state.</summary>
    public void ResetStatue()
    {
        foreach (StatuePart part in _parts.Values)
            part.ResetPart();
        _broken.Clear();
        _loose.Clear();
        _debris.Clear();
        _pending.Clear();
        _lean = 0f;
        _collapsed = false;
    }

    public string ProgressFor(WreckChallenge challenge)
    {
        var bits = new List<string>();
        if (challenge.RequiredParts.Length > 0)
        {
            bits.Add(string.Join("  ",
                challenge.RequiredParts.Select(p =>
                    $"{p}: {(_broken.Contains(p) ? "BROKEN" : "intact")}")));
        }
        bits.Add($"broken {_broken.Count} (goal {challenge.MinimumBrokenParts})");
        return string.Join("   |   ", bits);
    }

    public IReadOnlyList<StatuePart> SnapshotParts() => _parts.Values.ToList();

    /// <summary>Used by deterministic tests; no gameplay code depends on this.</summary>
    public bool DebugGraphValid => _graphValidated;
}
