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
    private readonly Dictionary<ulong, PendingVehicleContact> _pendingContacts = new();
    private readonly HashSet<string> _priorityParts = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, ulong> _lastVehicleImpactMsec = new();
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

    /// <summary>
    /// Authored hit volumes overlap during a fast arcade pass. Collect every
    /// candidate from this physics step and resolve one coherent contact region
    /// (both knees, the hat, the shield, or the chest) rather than breaking the
    /// whole statue through unrelated overlaps.
    /// </summary>
    public void ReportVehicleContact(StatuePart part, VehicleController vehicle,
        float energy, float speed, Vector3 direction, Vector3 hitPoint)
    {
        if (!_parts.ContainsKey(part.PartId) || part.IsBroken)
            return;
        ulong id = vehicle.GetInstanceId();
        bool hasPending = _pendingContacts.TryGetValue(id, out PendingVehicleContact? pending);
        if (!hasPending)
        {
            // The cooldown gates new impact groups, not the other contacts that
            // arrive in the same physics step. All of those must be accumulated
            // before the deferred grouped resolution runs.
            ulong now = Time.GetTicksMsec();
            if (_lastVehicleImpactMsec.TryGetValue(id, out ulong last) && now - last < 550)
                return;
            pending = new PendingVehicleContact(vehicle, energy, speed, direction, hitPoint);
            _pendingContacts[id] = pending;
            Callable.From(() => ResolveVehicleContact(id)).CallDeferred();
        }
        pending!.Add(part);
    }

    private void ResolveVehicleContact(ulong vehicleId)
    {
        if (!_pendingContacts.Remove(vehicleId, out PendingVehicleContact? pending))
            return;
        List<StatuePart> candidates = pending.Candidates
            .Where(p => !p.IsBroken && !p.IsLoose)
            .Distinct()
            .ToList();
        if (candidates.Count == 0)
            return;

        // Only components whose authored hit volumes actually overlapped this
        // bumper are candidates. Support collapse remains a separate authored
        // propagation after a direct break; challenges never target a part by
        // proximity or by a large rectangular area around the vehicle.
        List<StatuePart> selected = candidates;
        if (_priorityParts.Count > 0)
        {
            List<StatuePart> required = candidates
                .Where(p => _priorityParts.Contains(p.PartId))
                .ToList();
            // The armour is a physically contacted protector of the core, not a
            // free secondary target. Other unrelated parts hit by the bumper are
            // ignored for a named-target challenge.
            if (_priorityParts.Contains("Core"))
            {
                foreach (StatuePart candidate in candidates)
                {
                    if (candidate.PartId == "Armour" && !required.Contains(candidate))
                        required.Add(candidate);
                }
            }
            selected = required;
        }

        // When the bumper genuinely overlaps both the chest armour and the
        // exposed core, resolve the authored protector first so the core cannot
        // be destroyed before the armour rule has run.
        selected = selected
            .OrderBy(p => p.PartId == "Armour" ? 0 : p.PartId == "Core" ? 2 : 1)
            .ToList();

        // A core contact that arrives while the armour is still intact is a
        // blocked contact, not a free break-through. Armour can peel in this
        // group, but the core must be contacted again after it is exposed.
        bool armourAlreadyBroken = !_parts.TryGetValue("Armour", out StatuePart? armourPart) ||
                                armourPart.IsBroken || armourPart.IsLoose;
        foreach (StatuePart part in selected)
        {
            if (part.IsBroken)
                continue;
            if (part.PartId == "Core" && !armourAlreadyBroken)
            {
                PartBlocked?.Invoke("Core", "Armour");
                continue;
            }
            ResolveImpact(part.PartId, pending.Energy, pending.Speed,
                pending.Direction, pending.HitPoint);
        }

        // If the armour peeled in this group and the authored core hit volume is
        // still physically overlapping the bumper, that is a real follow-through
        // contact. Never break the core by proximity or by challenge identity.
        if (_priorityParts.Contains("Core") && !armourAlreadyBroken &&
            _parts.TryGetValue("Armour", out StatuePart? armour) && armour.IsBroken &&
            _parts.TryGetValue("Core", out StatuePart? core) && !core.IsBroken &&
            core.IsVehicleOverlapping(pending.Vehicle))
        {
            ResolveImpact("Core", pending.Energy, pending.Speed,
                pending.Direction, pending.HitPoint);
        }

        // The cooldown starts after the grouped contact has resolved. The same
        // physics step can still add all of its contacts to this group above.
        _lastVehicleImpactMsec[vehicleId] = Time.GetTicksMsec();
        pending.Vehicle.RegisterStatueSmash(pending.Energy, pending.Direction);
    }

    public void SetPriorityParts(IEnumerable<string> partIds)
    {
        _priorityParts.Clear();
        foreach (string id in partIds)
            if (!string.IsNullOrWhiteSpace(id))
                _priorityParts.Add(id);
    }

    private sealed class PendingVehicleContact
    {
        public readonly VehicleController Vehicle;
        public readonly float Energy;
        public readonly float Speed;
        public readonly Vector3 Direction;
        public readonly Vector3 HitPoint;
        public readonly List<StatuePart> Candidates = new();

        public PendingVehicleContact(VehicleController vehicle, float energy, float speed,
            Vector3 direction, Vector3 hitPoint)
        {
            Vehicle = vehicle;
            Energy = energy;
            Speed = speed;
            Direction = direction;
            HitPoint = hitPoint;
        }

        public void Add(StatuePart part)
        {
            if (!Candidates.Contains(part))
                Candidates.Add(part);
        }
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
        _pendingContacts.Clear();
        _lastVehicleImpactMsec.Clear();
        _lean = 0f;
        _collapsed = false;
    }

    public ObjectiveProgress GetObjectiveProgress(WreckChallenge challenge) =>
        ObjectiveProgressRules.Evaluate(challenge, _broken);

    public string ProgressFor(WreckChallenge challenge) =>
        GetObjectiveProgress(challenge).CombinedLabel;

    public IReadOnlyList<StatuePart> SnapshotParts() => _parts.Values.ToList();

    /// <summary>Used by deterministic tests; no gameplay code depends on this.</summary>
    public bool DebugGraphValid => _graphValidated;
}
