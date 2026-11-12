using System;
using System.Collections.Generic;
using Godot;

namespace RampWreckers;

/// <summary>
/// Arcade RigidBody3D vehicle. The script adds forces to the authored model and
/// invisible collider; it never creates visible geometry.
/// </summary>
public partial class VehicleController : RigidBody3D
{
    [Export] public int VehicleIndex { get; set; }
    [Export] public string DisplayName { get; set; } = "Vehicle";
    [Export] public float Acceleration { get; set; } = 18f;
    [Export] public float TopSpeed { get; set; } = 32f;
    [Export] public float ReverseTopSpeed { get; set; } = 10f;
    [Export] public float SteeringStrength { get; set; } = 5.5f;
    [Export] public float Grip { get; set; } = 7.0f;
    [Export] public float BrakeStrength { get; set; } = 4.2f;
    [Export] public float RollingResistance { get; set; } = 0.24f;
    [Export] public float AlignTorque { get; set; } = 8f;
    [Export] public float AirSteer { get; set; } = 0.45f;
    [Export] public float RollStability { get; set; } = 2.2f;
    [Export] public float SpawnHeight { get; set; } = 1.2f;
    [Export] public float MaxClimbSpeed { get; set; } = 45f;

    public event Action<VehicleController, Vector3, float>? HardImpact;
    public event Action<VehicleController>? BecameAirborne;
    public event Action<VehicleController>? Landed;

    public bool ControlsEnabled { get; set; } = true;
    public Vector3 PreSolveVelocity { get; private set; }
    /// <summary>Velocity sampled before the physics step, used for contact scoring.</summary>
    public Vector3 ImpactVelocity { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsAirborne => !IsGrounded;
    public float SpeedKph => LinearVelocity.Length() * 3.6f;
    public float ForwardSpeed
    {
        get
        {
            Vector3 forward = -GlobalTransform.Basis.Z;
            forward.Y = 0f;
            return forward.LengthSquared() > 0.001f
                ? LinearVelocity.Dot(forward.Normalized())
                : 0f;
        }
    }

    public int UpgradeEngineLevel { get; private set; }
    public int UpgradeGripLevel { get; private set; }
    public int PaintIndex { get; private set; }

    private readonly List<RayCast3D> _wheelProbes = new();
    private float _upsideDownTimer;
    private bool _wasGrounded;
    private float _controlGraceTimer;
    private ulong _lastHardImpactMsec;

    private static readonly Color[] PaintColors =
    {
        new(0.93f, 0.30f, 0.24f), // coral
        new(0.12f, 0.70f, 0.68f), // teal
        new(0.98f, 0.72f, 0.20f), // gold
        new(0.45f, 0.34f, 0.82f), // violet
        new(0.90f, 0.92f, 0.94f), // white
        new(0.22f, 0.48f, 0.92f), // blue
    };

    public override void _Ready()
    {
        ContactMonitor = true;
        MaxContactsReported = 24;
        ContinuousCd = true;
        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = new Vector3(0f, -0.28f, 0f);
        BodyEntered += OnBodyEntered;
        for (int i = 1; i <= 4; i++)
        {
            RayCast3D? probe = GetNodeOrNull<RayCast3D>($"WheelProbe{i}");
            if (probe != null)
            {
                probe.ExcludeParent = true;
                _wheelProbes.Add(probe);
            }
        }
        if (_wheelProbes.Count == 0)
        {
            RayCast3D? fallback = GetNodeOrNull<RayCast3D>("GroundProbe");
            if (fallback != null)
            {
                fallback.ExcludeParent = true;
                _wheelProbes.Add(fallback);
            }
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        PreSolveVelocity = state.LinearVelocity;
        // BodyEntered can miss very fast, single-step contacts (especially with
        // CCD). Scan contact points directly so every real statue strike is
        // scored exactly once by the part's own cooldown.
        int contacts = state.GetContactCount();
        for (int i = 0; i < contacts; i++)
        {
            GodotObject? collider = state.GetContactColliderObject(i);
            if (collider is StatuePart part)
            {
                Vector3 hitPoint = state.GetContactColliderPosition(i);
                part.ReportVehicleImpact(this, hitPoint);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        ImpactVelocity = LinearVelocity;
        ReadGround();
        if (_wasGrounded && !IsGrounded && ForwardSpeed > 8f && GlobalPosition.Z < 9f)
            BecameAirborne?.Invoke(this);
        if (!_wasGrounded && IsGrounded)
            Landed?.Invoke(this);
        _wasGrounded = IsGrounded;

        if (!ControlsEnabled)
        {
            // Coast to a stop when a menu is open instead of keeping throttle stuck.
            if (IsGrounded)
                ApplyCentralForce(-new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z) * Mass * 2.4f);
            return;
        }

        float throttle = Input.GetActionStrength("drive_forward") -
                         Input.GetActionStrength("drive_reverse");
        float steer = Input.GetActionStrength("steer_right") -
                      Input.GetActionStrength("steer_left");
        float engine = 1f + UpgradeEngineLevel * 0.14f;
        float grip = Grip * (1f + UpgradeGripLevel * 0.22f);

        Vector3 up = GlobalTransform.Basis.Y.Normalized();
        Vector3 forward = -GlobalTransform.Basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.001f)
            forward = Vector3.Back;
        forward = forward.Normalized();
        Vector3 right = GlobalTransform.Basis.X;
        right.Y = 0f;
        if (right.LengthSquared() < 0.001f)
            right = Vector3.Right;
        right = right.Normalized();

        Vector3 flatVelocity = new(LinearVelocity.X, 0f, LinearVelocity.Z);
        float forwardSpeed = flatVelocity.Dot(forward);
        float flatSpeed = flatVelocity.Length();
        Vector3 groundNormal = AverageGroundNormal();

        if (IsGrounded)
        {
            ApplyCentralForce(-groundNormal * Mass * 2.2f);
            ApplyCentralForce(-flatVelocity * Mass * RollingResistance);

            bool brake = Input.IsActionPressed("brake");
            if (brake)
                ApplyCentralForce(-flatVelocity * Mass * BrakeStrength);

            if (throttle > 0.02f && forwardSpeed < TopSpeed * engine)
            {
                Vector3 drive = (forward * 0.86f + groundNormal * 0.14f).Normalized();
                ApplyCentralForce(drive * (throttle * Mass * Acceleration * engine));
            }
            else if (throttle < -0.02f && forwardSpeed > -ReverseTopSpeed)
            {
                ApplyCentralForce(-forward * (-throttle * Mass * Acceleration * 0.68f));
            }

            // Lateral tyre grip. This makes steering authoritative rather than
            // letting the vehicle slide like a physics box.
            Vector3 lateral = right * flatVelocity.Dot(right);
            float lateralForce = Mathf.Clamp(lateral.Length() * grip, 0f, 42f);
            if (lateral.LengthSquared() > 0.0001f)
                ApplyCentralForce(-lateral.Normalized() * Mass * lateralForce);

            float authority = Mathf.Clamp(Mathf.Abs(forwardSpeed) / 6.5f, 0.08f, 1f);
            float reverseSign = forwardSpeed < -0.4f ? -1f : 1f;
            ApplyTorque(groundNormal * (-steer * reverseSign * Mass *
                SteeringStrength * authority));

            // Keep the chassis level with the ground; this is the main anti-flip
            // stability aid, not a replacement for real suspension travel.
            Vector3 align = up.Cross(groundNormal);
            ApplyTorque(align * (Mass * AlignTorque));

            float rollRate = GlobalTransform.Basis.Z.Normalized().Dot(AngularVelocity);
            ApplyTorque(-GlobalTransform.Basis.Z.Normalized() * (rollRate * Mass * RollStability));

            if (Mathf.Abs(forwardSpeed) > 0.2f)
            {
                Vector3 noseDown = -forward * (Mathf.Abs(forwardSpeed) * 0.018f);
                ApplyCentralForce(noseDown * Mass);
            }

            if (up.Dot(Vector3.Up) < 0.16f)
            {
                _upsideDownTimer += dt;
                if (_upsideDownTimer > 1.7f)
                {
                    RecoverUpright();
                    _upsideDownTimer = 0f;
                }
            }
            else
            {
                _upsideDownTimer = 0f;
            }
        }
        else
        {
            ApplyTorque(GlobalTransform.Basis.Y * (-steer * Mass * AirSteer));
            if (throttle > 0.02f)
                ApplyTorque(GlobalTransform.Basis.X * (-throttle * Mass * 0.55f));
            else if (throttle < -0.02f)
                ApplyTorque(GlobalTransform.Basis.X * (-throttle * Mass * 0.45f));
            ApplyTorque(-AngularVelocity * (Mass * 0.12f));
        }

        // Absolute safety clamps: a rescue from tunnelling or a physics explosion.
        if (LinearVelocity.Length() > MaxClimbSpeed)
            LinearVelocity = LinearVelocity.Normalized() * MaxClimbSpeed;
        if (AngularVelocity.Length() > 18f)
            AngularVelocity = AngularVelocity.Normalized() * 18f;
    }

    private void ReadGround()
    {
        Vector3 normalSum = Vector3.Zero;
        int hits = 0;
        foreach (RayCast3D probe in _wheelProbes)
        {
            probe.ForceRaycastUpdate();
            if (probe.IsColliding())
            {
                hits++;
                normalSum += probe.GetCollisionNormal();
            }
        }
        IsGrounded = hits > 0;
        _lastGroundNormal = hits > 0 ? normalSum.Normalized() : Vector3.Up;
    }

    private Vector3 _lastGroundNormal = Vector3.Up;
    private Vector3 AverageGroundNormal() =>
        _lastGroundNormal.LengthSquared() > 0.001f ? _lastGroundNormal : Vector3.Up;

    private void OnBodyEntered(Node body)
    {
        if (body is StatuePart statuePart)
            statuePart.ReportVehicleImpact(this);

        ulong now = Time.GetTicksMsec();
        float speed = PreSolveVelocity.Length();
        if (speed > 11f && now - _lastHardImpactMsec > 180)
        {
            _lastHardImpactMsec = now;
            HardImpact?.Invoke(this, GlobalPosition, speed);
        }
    }

    public void ResetTo(Transform3D spawn)
    {
        ControlsEnabled = false;
        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        Vector3 position = spawn.Origin + Vector3.Up * SpawnHeight;
        Basis basis = spawn.Basis.Orthonormalized();
        GlobalTransform = new Transform3D(basis, position);
        Sleeping = false;
        Freeze = false;
        ControlsEnabled = true;
        _upsideDownTimer = 0f;
        _wasGrounded = false;
        _controlGraceTimer = 0f;
    }

    /// <summary>Flip an upside-down vehicle back onto its wheels without changing position.</summary>
    public void RecoverUpright()
    {
        Vector3 forward = -GlobalTransform.Basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.001f)
            forward = Vector3.Back;
        forward = forward.Normalized();
        float yaw = Mathf.Atan2(-forward.X, -forward.Z);
        Basis basis = Basis.FromEuler(new Vector3(0f, yaw, 0f));
        Vector3 position = GlobalPosition + Vector3.Up * 1.7f;
        ControlsEnabled = false;
        Freeze = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GlobalTransform = new Transform3D(basis, position);
        Freeze = false;
        ControlsEnabled = true;
        _upsideDownTimer = 0f;
        GD.Print($"[Vehicle] {DisplayName} auto-recovered upright.");
    }

    public void ApplyProfile(int engineLevel, int gripLevel, int paintIndex, bool paintUnlocked)
    {
        UpgradeEngineLevel = Math.Clamp(engineLevel, 0, 1);
        UpgradeGripLevel = Math.Clamp(gripLevel, 0, 1);
        PaintIndex = Math.Clamp(paintIndex, 0, PaintColors.Length - 1);
        if (paintUnlocked)
            ApplyPaint(PaintColors[PaintIndex]);
    }

    public void ApplyPaint(Color color)
    {
        Node? art = GetNodeOrNull("Art");
        if (art == null)
            return;
        foreach (Node node in art.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh)
                continue;
            string name = mesh.Name.ToString();
            if (!IsBodyPaintSurface(name))
                continue;
            if (mesh.Mesh == null)
                continue;
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                Material? material = mesh.GetActiveMaterial(surface);
                if (material is not StandardMaterial3D original)
                    continue;
                var tinted = (StandardMaterial3D)original.Duplicate();
                tinted.AlbedoColor = color;
                mesh.SetSurfaceOverrideMaterial(surface, tinted);
            }
        }
    }

    private static bool IsBodyPaintSurface(string name) =>
        name.Contains("chassis", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("cab", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("roof", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("bonnet", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("bed_wall", StringComparison.OrdinalIgnoreCase);

    public string SpeedText => $"{SpeedKph:0} km/h";
    public Color CurrentPaintColor => PaintColors[PaintIndex];

    public static string PaintName(int index) => index switch
    {
        0 => "Coral",
        1 => "Teal",
        2 => "Gold",
        3 => "Violet",
        4 => "Pearl",
        _ => "Blue",
    };
}
