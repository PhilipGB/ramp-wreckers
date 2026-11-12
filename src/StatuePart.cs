using System;
using Godot;

namespace RampWreckers;

public enum ImpactResult
{
    Ignored,
    Resisted,
    Blocked,
    Broken,
    Collapsed,
}

/// <summary>
/// One authored statue component. It is frozen while intact, so it can hold up the
/// statue, and becomes a normal rigid body when its authored joint fails.
/// </summary>
public partial class StatuePart : RigidBody3D
{
    [Export] public string PartId { get; set; } = "";
    [Export] public float BreakThreshold { get; set; } = 100f;
    [Export] public int ScoreValue { get; set; } = 25;
    [Export] public bool IsSupport { get; set; }
    [Export] public string SupportedByCsv { get; set; } = "";
    [Export] public string ProtectedByCsv { get; set; } = "";
    [Export] public float WeakPointMultiplier { get; set; } = 1f;
    [Export] public bool CountsAsBrokenWhenUnsupported { get; set; } = true;
    [Export] public bool SolidWhenIntact { get; set; } = true;

    public bool IsBroken { get; private set; }
    public bool IsLoose { get; private set; }
    public Transform3D InitialLocalTransform { get; private set; }
    public string[] SupportedBy { get; private set; } = Array.Empty<string>();
    public string[] ProtectedBy { get; private set; } = Array.Empty<string>();

    private ulong _lastImpactMsec;
    private bool _initialCaptured;
    private Area3D? _hitArea;

    public override void _Ready()
    {
        Freeze = true;
        FreezeMode = FreezeModeEnum.Static;
        ContactMonitor = true;
        MaxContactsReported = 16;
        ContinuousCd = true;
        SupportedBy = SplitCsv(SupportedByCsv);
        ProtectedBy = SplitCsv(ProtectedByCsv);

        // Intact statues are real obstacles (world layer 1) so the car cannot
        // visually pass through them. The authored Area3D hit volume still
        // starts the arcade smash-through; once a component fails its rigid
        // body moves to the debris layer and the car can follow through.
        CollisionLayer = SolidWhenIntact ? 1u : 0u;
        CollisionMask = 0;
        _hitArea = GetNodeOrNull<Area3D>("HitArea");
        if (_hitArea == null)
        {
            GD.PushError($"{PartId}: missing authored HitArea child.");
        }
        else
        {
            _hitArea.CollisionLayer = 4;
            _hitArea.CollisionMask = 2;
            _hitArea.Monitorable = false;
            _hitArea.Monitoring = true;
            _hitArea.BodyEntered += OnHitAreaBodyEntered;
        }
        BodyEntered += OnBodyEntered;
        if (!_initialCaptured)
        {
            InitialLocalTransform = Transform;
            _initialCaptured = true;
        }
    }

    private void OnHitAreaBodyEntered(Node body)
    {
        if (body is VehicleController vehicle)
            ReportVehicleImpact(vehicle);
    }

    public void CaptureInitialTransform()
    {
        if (_initialCaptured)
            return;
        InitialLocalTransform = Transform;
        _initialCaptured = true;
    }

    private static string[] SplitCsv(string csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void OnBodyEntered(Node other)
    {
        if (other is VehicleController vehicle)
            ReportVehicleImpact(vehicle);
    }

    /// <summary>Called from a physics contact callback; damage is applied deferred.</summary>
    public void ReportVehicleImpact(VehicleController vehicle, Vector3? contactPoint = null)
    {
        if (!IsInsideTree())
            return;
        ulong now = Time.GetTicksMsec();
        if (now - _lastImpactMsec < 180)
            return;

        Vector3 velocity = vehicle.ImpactVelocity;
        if (velocity.LengthSquared() < vehicle.PreSolveVelocity.LengthSquared())
            velocity = vehicle.PreSolveVelocity;
        if (velocity.LengthSquared() < 0.01f)
            velocity = vehicle.LinearVelocity;
        float speed = velocity.Length();
        if (speed < ImpactModel.MinimumImpactSpeed)
            return;

        _lastImpactMsec = now;
        float energy = ImpactModel.Energy(vehicle.Mass, speed);
        Vector3 direction = velocity.Normalized();
        Vector3 hitPoint = contactPoint ?? GlobalPosition.Lerp(vehicle.GlobalPosition, 0.25f);
        if (GetParent() is StatueController owner)
            owner.ReportVehicleContact(this, vehicle, energy, speed, direction, hitPoint);
    }

    /// <summary>True when the authored hit volume currently overlaps the vehicle.</summary>
    public bool IsVehicleOverlapping(VehicleController vehicle)
    {
        if (_hitArea == null || !_hitArea.Monitoring)
            return false;
        foreach (Node3D body in _hitArea.GetOverlappingBodies())
            if (body == vehicle)
                return true;
        return false;
    }

    public void Release(Vector3 impulse, Vector3 torqueImpulse, bool loose)
    {
        if (IsBroken)
            return;
        IsBroken = !loose;
        IsLoose = loose;
        Freeze = false;
        Sleeping = false;
        CollisionLayer = 4;
        CollisionMask = 7;
        ContinuousCd = true;
        if (_hitArea != null)
            _hitArea.Monitoring = false;
        ApplyCentralImpulse(impulse * Mass);
        ApplyTorqueImpulse(torqueImpulse * Mass);
    }

    public void ResetPart()
    {
        Freeze = true;
        FreezeMode = FreezeModeEnum.Static;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        Transform = InitialLocalTransform;
        IsBroken = false;
        IsLoose = false;
        Sleeping = false;
        Visible = true;
        CollisionLayer = SolidWhenIntact ? 1u : 0u;
        CollisionMask = 0;
        if (_hitArea != null)
            _hitArea.Monitoring = true;
        _lastImpactMsec = 0;
    }

    public void SetPosedTransform(Transform3D pose)
    {
        if (!IsBroken && !IsLoose)
            Transform = pose;
    }

    public override string ToString() =>
        $"{PartId}(broken={IsBroken}, loose={IsLoose}, threshold={BreakThreshold:0})";
}
