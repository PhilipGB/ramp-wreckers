using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RampWreckers;

/// <summary>
/// The playable vertical slice: drive, adjust the ramp, smash authored statues,
/// complete nine objectives, earn coins and keep progress across sessions.
/// </summary>
public partial class GameRoot : Node3D
{
    private static readonly string[] VehicleScenes =
    {
        "res://scenes/vehicles/RallyCar.tscn",
        "res://scenes/vehicles/PickupTruck.tscn",
        "res://scenes/vehicles/MonsterTruck.tscn",
    };

    private static readonly string[] VehicleNames = { "Rally Car", "Pickup Truck", "Monster Truck" };
    private static readonly string[] StatueScenes =
    {
        "res://scenes/statues/Gnome.tscn",
        "res://scenes/statues/Knight.tscn",
        "res://scenes/statues/Robot.tscn",
    };

    private Node3D _actors = null!;
    private Node3D _statueAnchor = null!;
    private StaticBody3D _ramp = null!;
    private Marker3D _vehicleSpawn = null!;
    private Camera3D _camera = null!;
    private AudioStreamPlayer _crashAudio = null!;
    private AudioStreamPlayer _rewardAudio = null!;
    private AudioStreamPlayer _launchAudio = null!;
    private AudioStreamPlayer _engineAudio = null!;
    private OmniLight3D _statueLight = null!;
    private ImpactEffects _effects = null!;
    private Label _modeLabel = null!;
    private Label _challengeLabel = null!;
    private Label _goalLabel = null!;
    private Label _requiredLabel = null!;
    private Label _totalLabel = null!;
    private ProgressBar _requiredBar = null!;
    private ProgressBar _totalBar = null!;
    private Label _statsLabel = null!;
    private Label _statusLabel = null!;
    private Label _speedLabel = null!;
    private Label _gearLabel = null!;
    private ProgressBar _speedBar = null!;
    private Label _bannerLabel = null!;
    private Control _bannerPanel = null!;
    private Label _saveBanner = null!;
    private Control _garage = null!;
    private Control _helpPanel = null!;
    private Button _helpClose = null!;
    private ColorRect _modalBackdrop = null!;
    private bool _helpOpen;
    private Label _garageCoins = null!;
    private Button _rallyButton = null!;
    private Button _pickupButton = null!;
    private Button _monsterButton = null!;
    private Button _engineButton = null!;
    private Button _gripButton = null!;
    private Button _paintButton = null!;
    private Label _garageHint = null!;
    private Button _qualityButton = null!;
    private Button _volumeButton = null!;

    private GameProfile _profile = null!;
    private VehicleController? _vehicle;
    private StatueController? _statue;
    private int _vehicleIndex = 1;
    private int _challengeIndex;
    private int _statueIndex;
    private int _spawnedStatueIndex = -1;
    private bool _freeSmash;
    private bool _garageOpen;
    private bool _runStarted;
    private bool _challengeCompletedThisRun;
    private bool _rewardThisRun;
    private int _runScore;
    private float _runTime;
    private int _attempts;
    private float _rampDegrees = 16f;
    private string _message = "Pick a challenge, raise the ramp with Q/E, then hold W to launch!";
    private float _bannerTimer;
    private float _saveBannerTimer;
    private bool _saveBannerSticky;
    private bool _audioAvailable;

    private int _cameraPreset;
    private float _camYaw;
    private float _camPitch;
    private float _camDistanceFactor = 1f;
    private bool _orbiting;
    private bool _cameraInitialized;
    private bool _showcaseCamera;
    private float _shake;
    private float _slowmoTimer;
    private float _cinematicTimer;
    private bool _firstBreakCamera;
    private bool _cinematicSnap;
    private float _hudRefreshTimer;
    private Vector3 _impactFocus;

    public VehicleController? CurrentVehicle => _vehicle;
    public StatueController? CurrentStatue => _statue;
    public GameProfile Profile => _profile;
    public bool IsFreeSmash => _freeSmash;
    public int CurrentChallengeIndex => _challengeIndex;
    public int CurrentVehicleIndex => _vehicleIndex;

    public override void _Ready()
    {
        ConfigureInput();
        _actors = GetNode<Node3D>("Actors");
        _statueAnchor = GetNode<Node3D>("StatueAnchor");
        _ramp = GetNode<StaticBody3D>("RampPivot");
        _vehicleSpawn = GetNode<Marker3D>("VehicleSpawn");
        _camera = GetNode<Camera3D>("FollowCamera");
        _crashAudio = GetNode<AudioStreamPlayer>("CrashAudio");
        _rewardAudio = GetNode<AudioStreamPlayer>("RewardAudio");
        _launchAudio = GetNode<AudioStreamPlayer>("LaunchAudio");
        _engineAudio = GetNode<AudioStreamPlayer>("EngineAudio");
        _statueLight = GetNode<OmniLight3D>("StatueLight");
        _effects = GetNode<ImpactEffects>("Effects");

        _modeLabel = GetNode<Label>("Hud/ObjectivePanel/ObjectiveVBox/ModeRow/Mode");
        _challengeLabel = GetNode<Label>("Hud/ObjectivePanel/ObjectiveVBox/Challenge");
        _goalLabel = GetNode<Label>("Hud/ObjectivePanel/ObjectiveVBox/Goal");
        _requiredLabel = GetNode<Label>("Hud/ObjectivePanel/ObjectiveVBox/Required");
        _requiredBar = GetNode<ProgressBar>("Hud/ObjectivePanel/ObjectiveVBox/RequiredBar");
        _totalLabel = GetNode<Label>("Hud/ObjectivePanel/ObjectiveVBox/Total");
        _totalBar = GetNode<ProgressBar>("Hud/ObjectivePanel/ObjectiveVBox/TotalBar");
        _statsLabel = GetNode<Label>("Hud/StatsPanel/StatsHBox/Stats");
        _statusLabel = GetNode<Label>("Hud/ObjectivePanel/ObjectiveVBox/Status");
        _speedLabel = GetNode<Label>("Hud/SpeedPanel/SpeedVBox/Speed");
        _gearLabel = GetNode<Label>("Hud/SpeedPanel/SpeedVBox/Gear");
        _speedBar = GetNode<ProgressBar>("Hud/SpeedPanel/SpeedVBox/SpeedBar");
        _bannerPanel = GetNode<Control>("Hud/Banner");
        _bannerLabel = GetNode<Label>("Hud/Banner/BannerLabel");
        _saveBanner = GetNode<Label>("Hud/SaveBanner");
        _garage = GetNode<Control>("Hud/Garage");
        _helpPanel = GetNode<Control>("Hud/HelpPanel");
        _helpClose = GetNode<Button>("Hud/HelpPanel/HelpVBox/HelpClose");
        _modalBackdrop = GetNode<ColorRect>("Hud/ModalBackdrop");
        _garageCoins = GetNode<Label>("Hud/Garage/GarageVBox/GarageCoins");
        _rallyButton = GetNode<Button>("Hud/Garage/GarageVBox/VehicleRow/RallyButton");
        _pickupButton = GetNode<Button>("Hud/Garage/GarageVBox/VehicleRow/PickupButton");
        _monsterButton = GetNode<Button>("Hud/Garage/GarageVBox/VehicleRow/MonsterButton");
        _engineButton = GetNode<Button>("Hud/Garage/GarageVBox/UpgradeRow/EngineButton");
        _gripButton = GetNode<Button>("Hud/Garage/GarageVBox/UpgradeRow/GripButton");
        _paintButton = GetNode<Button>("Hud/Garage/GarageVBox/UpgradeRow/PaintButton");
        _garageHint = GetNode<Label>("Hud/Garage/GarageVBox/GarageHint");
        _qualityButton = GetNode<Button>("Hud/Garage/GarageVBox/SettingsRow/QualityButton");
        _volumeButton = GetNode<Button>("Hud/Garage/GarageVBox/SettingsRow/VolumeButton");

        var catalogErrors = new List<string>();
        if (!ChallengeCatalog.TryValidate(out catalogErrors))
            foreach (string error in catalogErrors)
                GD.PushError("[ChallengeCatalog] " + error);

        string savePath = OS.GetEnvironment("RW_SAVE_PATH");
        if (string.IsNullOrWhiteSpace(savePath))
            savePath = ProjectSettings.GlobalizePath("user://ramp_wreckers_save.json");
        var store = new SaveStore(savePath);
        _profile = new GameProfile(store);
        _cameraPreset = _profile.Data.CameraPreset;
        if (_profile.LoadHadProblem)
        {
            PushSaveBanner(_profile.LoadMessage, sticky: true);
            GD.PushWarning("[Save] " + _profile.LoadMessage);
        }
        if (!string.IsNullOrEmpty(_profile.LoadMessage) && !_profile.LoadHadProblem)
            GD.Print("[Save] " + _profile.LoadMessage);
        if (catalogErrors.Count == 0)
            GD.Print("[Catalog] 9 authored challenges validated against statue part inventories.");

        _audioAvailable = !string.Equals(AudioServer.GetDriverName(), "Dummy",
            StringComparison.OrdinalIgnoreCase);
        ConfigureEngineAudio();
        ApplySettings();
        _garage.Visible = false;
        _helpPanel.Visible = false;
        _modalBackdrop.Visible = false;
        ConnectGarageButtons();
        UpdateRamp();
        SpawnVehicle(reset: false);
        ResetRun(respawnVehicle: true, respawnStatue: true, countAttempt: false);
        _cameraInitialized = false;
        _message = "Hold W to launch. Q/E changes the ramp. R retries instantly.";
        RefreshHud(force: true);
    }

    public override void _ExitTree()
    {
        if (_profile != null)
            _profile.TrySave();
        StopAudio();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            _profile?.TrySave();
            StopAudio();
            GetTree().Quit();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        HandleGameplayInput();
        UpdateAudio(dt);
        UpdateCamera(dt);
        UpdateTimers(dt);

        if (!_garageOpen && _vehicle != null)
        {
            if (!_runStarted && _vehicle.GlobalPosition.DistanceTo(_vehicleSpawn.GlobalPosition) > 1.4f)
                _runStarted = true;
            if (_runStarted)
                _runTime += dt;

            if (_vehicle.GlobalPosition.Y < -6f || Mathf.Abs(_vehicle.GlobalPosition.X) > 55f ||
                Mathf.Abs(_vehicle.GlobalPosition.Z) > 75f)
            {
                _message = "Out of bounds — retrying.";
                ResetRun(true, true, true);
            }
        }

        _hudRefreshTimer -= dt;
        if (_hudRefreshTimer <= 0f)
        {
            _hudRefreshTimer = 0.07f;
            RefreshHud(force: false);
        }
    }

    private void HandleGameplayInput()
    {
        if (Input.IsActionJustPressed("help"))
        {
            ToggleHelp();
            return;
        }
        if (Input.IsActionJustPressed("garage"))
        {
            ToggleGarage();
            return;
        }
        if (Input.IsActionJustPressed("pause"))
        {
            if (_helpOpen)
                ToggleHelp(false);
            else if (_garageOpen)
                ToggleGarage(false);
            return;
        }
        if (_garageOpen || _helpOpen)
            return;

        if (Input.IsActionJustPressed("retry"))
            ResetRun(respawnVehicle: true, respawnStatue: true, countAttempt: true);
        if (Input.IsActionJustPressed("vehicle_one"))
            SelectVehicle(0);
        if (Input.IsActionJustPressed("vehicle_two"))
            SelectVehicle(1);
        if (Input.IsActionJustPressed("vehicle_three"))
            SelectVehicle(2);
        if (Input.IsActionJustPressed("previous_challenge"))
            ChangeChallenge(-1);
        if (Input.IsActionJustPressed("next_challenge"))
            ChangeChallenge(1);
        if (Input.IsActionJustPressed("free_smash"))
            ToggleFreeSmash();
        if (Input.IsActionJustPressed("ramp_down"))
            AdjustRamp(-2f);
        if (Input.IsActionJustPressed("ramp_up"))
            AdjustRamp(2f);
        if (Input.IsActionJustPressed("camera_toggle"))
        {
            _cinematicTimer = 0f;
            _cameraPreset = (_cameraPreset + 1) % 3;
            _profile.Data.CameraPreset = _cameraPreset;
            _profile.TrySave();
            _message = $"Camera: {CameraName(_cameraPreset)}";
        }
        if (Input.IsActionJustPressed("paint") && _profile.Data.PaintUnlocked)
        {
            _profile.Data.PaintIndex = (_profile.Data.PaintIndex + 1) % 6;
            _vehicle?.ApplyProfile(_profile.Data.EngineLevel, _profile.Data.GripLevel,
                _profile.Data.PaintIndex, true);
            _profile.TrySave();
            _message = "Paint: " + VehicleController.PaintName(_profile.Data.PaintIndex);
        }
        if (Input.IsActionJustPressed("quality_toggle"))
            CycleQuality();
        if (Input.IsActionJustPressed("volume_toggle"))
            CycleVolume();

    }

    private void UpdateTimers(float dt)
    {
        if (_bannerTimer > 0f)
        {
            _bannerTimer -= dt;
            if (_bannerTimer <= 0f)
                _bannerPanel.Visible = false;
        }
        if (_saveBannerTimer > 0f && !_saveBannerSticky)
        {
            _saveBannerTimer -= dt;
            if (_saveBannerTimer <= 0f)
                _saveBanner.Visible = false;
        }

        if (_slowmoTimer > 0f)
        {
            _slowmoTimer -= dt;
            Engine.TimeScale = _slowmoTimer > 0f ? 0.38f : 1.0f;
        }
        else
        {
            Engine.TimeScale = 1.0;
        }
        if (_cinematicTimer > 0f)
            _cinematicTimer -= dt;

        _shake = Mathf.Max(0f, _shake - dt * 1.8f);
        if (_vehicle != null && _vehicle.IsGrounded && _vehicle.SpeedKph > 12f)
            _shake = Mathf.Max(_shake, 0.03f);
    }

    private void UpdateAudio(float dt)
    {
        if (_vehicle == null || !_audioAvailable)
            return;
        float speed = Mathf.Abs(_vehicle.ForwardSpeed);
        float throttle = Input.GetActionStrength("drive_forward");
        bool grounded = _vehicle.IsGrounded;
        float targetPitch = 0.72f + Mathf.Clamp(speed / 36f, 0f, 1.15f) * 0.75f +
                            (grounded ? throttle * 0.18f : 0.25f);
        _engineAudio.PitchScale = Mathf.Lerp(_engineAudio.PitchScale, targetPitch,
            Mathf.Clamp(dt * 5f, 0f, 1f));
        float targetVolume = -15f + Mathf.Clamp(speed / 30f, 0f, 1f) * 10f +
                             (grounded ? 0f : -3f);
        _engineAudio.VolumeDb = Mathf.Lerp(_engineAudio.VolumeDb, targetVolume,
            Mathf.Clamp(dt * 4f, 0f, 1f));
        if (!_engineAudio.Playing)
            _engineAudio.Play();
    }

    private void UpdateCamera(float dt)
    {
        if (_showcaseCamera)
            return;
        VehicleController? vehicle = _vehicle;
        if (vehicle == null)
            return;

        Vector3 focus = vehicle.GlobalPosition + Vector3.Up * 1.0f;
        Vector3 forward = -vehicle.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.001f)
            forward = Vector3.Back;
        forward = forward.Normalized();

        bool cinematic = _cinematicTimer > 0f && _statue != null;
        Vector3 desired;
        Vector3 lookAt;
        if (cinematic)
        {
            if (_impactFocus.LengthSquared() < 0.01f)
                _impactFocus = _statueAnchor.GlobalPosition + Vector3.Up * 3.2f;
            Vector3[] candidates =
            {
                new(7.8f, 7.4f, 4.0f),
                new(-7.8f, 7.4f, 4.0f),
                new(0f, 8.5f, 5.0f),
                new(0f, 10.0f, 9.0f),
                new(6.5f, 7.2f, -6.0f),
                new(-6.5f, 7.2f, -6.0f),
            };
            desired = _impactFocus + candidates[0];
            bool found = false;
            var spaceCinematic = GetWorld3D().DirectSpaceState;
            foreach (Vector3 candidate in candidates)
            {
                Vector3 tryPosition = _impactFocus + candidate;
                var probe = PhysicsRayQueryParameters3D.Create(_impactFocus, tryPosition, 0x15);
                probe.CollideWithAreas = false;
                probe.CollideWithBodies = true;
                var probeHit = spaceCinematic.IntersectRay(probe);
                if (probeHit.Count == 0)
                {
                    desired = tryPosition;
                    found = true;
                    break;
                }
            }
            if (!found)
                desired = _impactFocus + new Vector3(0f, 12f, 20f);
            lookAt = _impactFocus;
        }
        else if (_cameraPreset == 2)
        {
            desired = vehicle.GlobalPosition + forward * 0.45f + Vector3.Up * 1.45f;
            lookAt = vehicle.GlobalPosition + forward * 16f + Vector3.Up * 1.3f;
        }
        else
        {
            float distance = (_cameraPreset == 1 ? 17.5f : 11.5f) * _camDistanceFactor;
            float height = (_cameraPreset == 1 ? 8.0f : 5.6f) + _camPitch * 4.5f;
            Basis yawBasis = new(Vector3.Up, _camYaw);
            Vector3 back = yawBasis * (-forward);
            desired = focus + back * distance + Vector3.Up * height;
            lookAt = focus + forward * (_cameraPreset == 1 ? 1.5f : 3.8f) + Vector3.Up * 0.35f;
        }

        if (!cinematic && _statue != null)
        {
            float nearest = NearestDebrisDistance(desired);
            if (nearest < 6.5f)
            {
                desired += Vector3.Up * 4.5f;
                desired += (desired - focus).Normalized() * 3.0f;
                if (NearestDebrisDistance(desired) < 6.5f)
                    desired += Vector3.Up * 5f;
            }
        }

        // Pull the camera in front of arena / statue geometry rather than clipping.
        var space = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(focus, desired, 0x15);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { vehicle.GetRid() };
        var hit = space.IntersectRay(query);
        if (hit.Count > 0 && hit.TryGetValue("position", out Variant positionVariant))
        {
            Vector3 hitPosition = positionVariant.AsVector3();
            desired = hitPosition + (focus - desired).Normalized() * 0.45f;
        }

        // Never let a chase camera end up inside the car, the statue platform or
        // below the arena. A short pull-in ray used to collapse the camera onto
        // the focus point when the vehicle was wedged against the pedestal.
        if (desired.DistanceTo(focus) < 3.0f)
            desired = focus + Vector3.Up * 6.2f - forward * 9.0f;
        desired.Y = Mathf.Max(desired.Y, 2.35f);
        desired.X = Mathf.Clamp(desired.X, -48f, 48f);
        desired.Z = Mathf.Clamp(desired.Z, -70f, 70f);
        if (desired.DistanceTo(lookAt) < 1.0f)
            desired = lookAt + Vector3.Up * 5.5f - forward * 7.0f;

        if (_shake > 0.001f)
        {
            float shake = _shake * 0.5f;
            desired += new Vector3((float)GD.RandRange(-shake, shake),
                (float)GD.RandRange(-shake, shake),
                (float)GD.RandRange(-shake, shake));
        }

        float smoothing = _cameraInitialized ? 1f - Mathf.Exp(-dt * 4.5f) : 1f;
        if (cinematic && _cinematicSnap)
        {
            smoothing = 1f;
            _cinematicSnap = false;
        }
        _camera.GlobalPosition = _camera.GlobalPosition.Lerp(desired, smoothing);
        _camera.LookAt(lookAt, Vector3.Up);
        float targetFov = 72f + Mathf.Clamp(vehicle.SpeedKph / 140f, 0f, 1f) * 6f;
        _camera.Fov = Mathf.Lerp(_camera.Fov, targetFov, Mathf.Clamp(dt * 3f, 0f, 1f));
        _cameraInitialized = true;
    }

    private float NearestDebrisDistance(Vector3 position)
    {
        if (_statue == null)
            return 999f;
        float best = 999f;
        foreach (StatuePart part in _statue.SnapshotParts())
        {
            if (!part.IsBroken && !part.IsLoose)
                continue;
            best = Mathf.Min(best, position.DistanceTo(part.GlobalPosition));
        }
        return best;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Right)
        {
            _orbiting = button.Pressed;
            if (button.Pressed)
                Input.MouseMode = Input.MouseModeEnum.Captured;
            else
                Input.MouseMode = Input.MouseModeEnum.Visible;
            return;
        }
        if (@event is InputEventMouseMotion motion && _orbiting)
        {
            _camYaw -= motion.Relative.X * 0.006f;
            _camPitch = Mathf.Clamp(_camPitch - motion.Relative.Y * 0.004f, -0.55f, 0.6f);
        }
        if (@event is InputEventMouseButton wheel && wheel.Pressed &&
            wheel.ButtonIndex == MouseButton.WheelUp)
            _camDistanceFactor = Mathf.Clamp(_camDistanceFactor - 0.08f, 0.65f, 1.8f);
        else if (@event is InputEventMouseButton wheelDown && wheelDown.Pressed &&
                 wheelDown.ButtonIndex == MouseButton.WheelDown)
            _camDistanceFactor = Mathf.Clamp(_camDistanceFactor + 0.08f, 0.65f, 1.8f);
    }

    private void SelectVehicle(int index)
    {
        if (index < 0 || index >= VehicleScenes.Length)
            return;
        if (!_profile.Data.VehiclesUnlocked[index])
        {
            _message = $"{VehicleNames[index]} is locked. Open the garage (G) and buy it for {GameProfile.MonsterTruckCost} coins.";
            RefreshHud(force: true);
            return;
        }
        if (_vehicleIndex == index)
        {
            ResetRun(true, true, true);
            return;
        }
        _vehicleIndex = index;
        SpawnVehicle(reset: true);
        ResetRun(true, true, true);
        _message = $"Vehicle: {VehicleNames[index]} — {VehicleRole(index)}";
        RefreshHud(force: true);
    }

    private static string VehicleRole(int index) => index switch
    {
        0 => "fast and light: long jumps",
        1 => "balanced: the all-rounder",
        _ => "heavy: shoves foundations",
    };

    private void SpawnVehicle(bool reset)
    {
        _vehicle?.QueueFree();
        _vehicle = null;
        PackedScene scene = GD.Load<PackedScene>(VehicleScenes[_vehicleIndex]);
        _vehicle = scene.Instantiate<VehicleController>();
        _actors.AddChild(_vehicle);
        _vehicle.ApplyProfile(_profile.Data.EngineLevel, _profile.Data.GripLevel,
            _profile.Data.PaintIndex, _profile.Data.PaintUnlocked);
        _vehicle.HardImpact += OnHardImpact;
        _vehicle.BecameAirborne += OnBecameAirborne;
        _vehicle.Landed += OnLanded;
        if (reset)
            _vehicle.ResetTo(_vehicleSpawn.GlobalTransform);
        _cameraInitialized = false;
    }

    private void SpawnStatue(bool force = false)
    {
        int statueIndex = _freeSmash ? _statueIndex : ChallengeCatalog.All[_challengeIndex].StatueIndex;
        if (!force && _statue != null && _spawnedStatueIndex == statueIndex)
        {
            _statue.SetPriorityParts(_freeSmash
                ? Array.Empty<string>()
                : ChallengeCatalog.All[_challengeIndex].RequiredParts);
            _statueLight.LightColor = statueIndex switch
            {
                0 => new Color(1.0f, 0.56f, 0.26f),
                1 => new Color(0.38f, 0.66f, 1.0f),
                _ => new Color(0.12f, 0.92f, 0.82f),
            };
            _statue.ResetStatue();
            return;
        }

        _statue?.QueueFree();
        _statue = null;
        _statueAnchor.Visible = true;
        PackedScene scene = GD.Load<PackedScene>(StatueScenes[statueIndex]);
        _statue = scene.Instantiate<StatueController>();
        _statueAnchor.AddChild(_statue);
        _statue.SetPriorityParts(_freeSmash
            ? Array.Empty<string>()
            : ChallengeCatalog.All[_challengeIndex].RequiredParts);
        _statueLight.LightColor = statueIndex switch
        {
            0 => new Color(1.0f, 0.56f, 0.26f),
            1 => new Color(0.38f, 0.66f, 1.0f),
            _ => new Color(0.12f, 0.92f, 0.82f),
        };
        _spawnedStatueIndex = statueIndex;
        _statue.PartBroken += OnPartBroken;
        _statue.PartBlocked += OnPartBlocked;
        _statue.PartResisted += OnPartResisted;
        _statue.PartLostSupport += OnPartLostSupport;
        _statue.Collapsed += OnStatueCollapsed;
        _statue.Leaned += OnStatueLeaned;
    }

    private void ResetRun(bool respawnVehicle, bool respawnStatue, bool countAttempt)
    {
        if (respawnStatue)
            SpawnStatue(force: false);
        else if (_statue != null)
            _statue.ResetStatue();

        VehicleController? vehicle = _vehicle;
        if (vehicle != null)
            vehicle.ResetTo(_vehicleSpawn.GlobalTransform);

        _runScore = 0;
        _runTime = 0f;
        _runStarted = false;
        _challengeCompletedThisRun = false;
        _rewardThisRun = false;
        if (countAttempt)
            _attempts++;
        _slowmoTimer = 0f;
        _cinematicTimer = 0f;
        _firstBreakCamera = false;
        _cinematicSnap = false;
        _showcaseCamera = false;
        Engine.TimeScale = 1.0;
        _shake = 0f;
        _impactFocus = Vector3.Zero;
        _bannerPanel.Visible = false;
        _message = _freeSmash
            ? "Free Smash — no rewards, unlimited retries. Go wild!"
            : "Ready! " + ChallengeCatalog.All[_challengeIndex].Hint;
        RefreshHud(force: true);
    }

    private void ChangeChallenge(int direction)
    {
        if (_freeSmash)
        {
            _statueIndex = (_statueIndex + direction + StatueScenes.Length) % StatueScenes.Length;
            ResetRun(true, true, true);
            _message = "Free Smash target: " + StatueCatalog.Names[_statueIndex];
            RefreshHud(force: true);
            return;
        }
        _challengeIndex = (_challengeIndex + direction + ChallengeCatalog.All.Length) %
                          ChallengeCatalog.All.Length;
        WreckChallenge selected = ChallengeCatalog.All[_challengeIndex];
        _rampDegrees = selected.RecommendedRampDegrees;
        UpdateRamp();
        _runScore = 0;
        _runTime = 0f;
        _attempts = 0;
        ResetRun(true, true, true);
        _message = $"Challenge {_challengeIndex + 1}/9 — {selected.Name}. Suggested ramp {_rampDegrees:0}°.";
        RefreshHud(force: true);
    }

    private void ToggleFreeSmash()
    {
        _freeSmash = !_freeSmash;
        if (_freeSmash)
            _statueIndex = ChallengeCatalog.All[_challengeIndex].StatueIndex;
        ResetRun(true, true, true);
        _message = _freeSmash
            ? "FREE SMASH: rewards off, every locally unlocked vehicle and ramp angle available."
            : $"Campaign challenge {_challengeIndex + 1}/9 resumed.";
        RefreshHud(force: true);
    }

    private void AdjustRamp(float degrees)
    {
        _rampDegrees = Mathf.Clamp(_rampDegrees + degrees, 10f, 27f);
        UpdateRamp();
        _message = $"Ramp angle: {_rampDegrees:0}°   (estimated launch {EstimatedLaunch():0.0} m)";
        RefreshHud(force: true);
    }

    private float EstimatedLaunch()
    {
        float speed = _vehicle != null
            ? Mathf.Max(14f, Mathf.Abs(_vehicle.ForwardSpeed))
            : 20f;
        return ImpactModel.EstimatedLaunchHeight(_rampDegrees, speed,
            (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 20.0));
    }

    private void UpdateRamp()
    {
        _ramp.Rotation = new Vector3(Mathf.DegToRad(_rampDegrees), 0f, 0f);
    }

    private void OnPartBroken(string partId, int points, bool weakPoint)
    {
        _runScore += points;
        Vector3 partPosition = _statue?.GetPart(partId)?.GlobalPosition ?? _statueAnchor.GlobalPosition;
        // Push the burst toward the chase camera so the car cannot hide the
        // contact completely, while keeping it near the broken component.
        Vector3 effectPosition = partPosition + Vector3.Up * 0.75f + Vector3.Back * 1.1f;
        float strength = Mathf.Clamp(points / 110f, 0.65f, 1.8f);
        _effects.SpawnDust(effectPosition, strength * 0.85f, weakPoint ? 3 : 3);
        _effects.SpawnChips(effectPosition, strength * 0.9f, weakPoint ? 6 : 4);
        _effects.SpawnSparks(effectPosition,
            weakPoint ? new Color(0.35f, 1.0f, 1.0f) : new Color(1.0f, 0.72f, 0.28f),
            weakPoint ? 5 : 2);
        _effects.SpawnFlash(effectPosition,
            weakPoint ? new Color(0.15f, 0.95f, 1.0f) : new Color(1.0f, 0.62f, 0.18f),
            weakPoint ? 5.0f : 2.5f);
        if (!_firstBreakCamera)
        {
            _firstBreakCamera = true;
            _impactFocus = partPosition.Lerp(_statueAnchor.GlobalPosition + Vector3.Up * 3.0f, 0.35f);
            _cinematicTimer = Mathf.Max(_cinematicTimer, 0.5f);
            _cinematicSnap = true;
        }
        _shake = Mathf.Max(_shake, weakPoint ? 0.38f : 0.24f);
        _crashAudio.PitchScale = (float)GD.RandRange(0.82, 1.12);
        _crashAudio.VolumeDb = -5f;
        _crashAudio.Play();

        if (weakPoint)
        {
            _slowmoTimer = 0.45f;
            _cinematicTimer = 0.7f;
            _message = $"WEAK POINT! {partId} shattered for {points} points. C skips the close-up.";
        }
        else
        {
            _message = $"{partId} smashed! +{points} impact score.";
        }

        if (_freeSmash)
        {
            RefreshHud(force: true);
            return;
        }

        WreckChallenge challenge = ChallengeCatalog.All[_challengeIndex];
        if (_challengeCompletedThisRun || _statue == null ||
            !ChallengeCatalog.IsComplete(challenge, _statue.BrokenPartIds))
        {
            RefreshHud(force: true);
            return;
        }

        GD.Print($"[CHALLENGE] {challenge.Name} complete? broken={string.Join(",", _statue.BrokenPartIds)} " +
                 $"required={string.Join(",", challenge.RequiredParts)} min={challenge.MinimumBrokenParts}");
        Medal medal = ChallengeCatalog.MedalFor(challenge, _statue.BrokenPartIds,
            _runScore, _runTime);
        RewardResult reward = _profile.RecordChallenge(_challengeIndex, medal);
        bool saved = _profile.TrySave();
        _challengeCompletedThisRun = true;
        _rewardThisRun = reward.FirstClear;
        _rewardAudio.Play();
        _message = reward.Message + (saved ? "" : "  SAVE FAILED: " + _profile.LastSaveError);
        ShowBanner($"CHALLENGE COMPLETE!\n{challenge.Name}\n" +
                   $"{ChallengeCatalog.MedalLabel(medal)}" +
                   (reward.FirstClear ? $"   +{reward.Coins} Wreck Coins" : ""), 4.5f);
        if (!saved)
            PushSaveBanner("Save failed: " + _profile.LastSaveError, sticky: true);
        RefreshHud(force: true);
    }

    private void OnPartBlocked(string partId, string protectorId)
    {
        _message = $"{protectorId} absorbed the hit — the {partId} is still protected!";
        _effects.SpawnChips(_statueAnchor.GlobalPosition + Vector3.Up * 4.2f, 0.55f, 3);
        _effects.SpawnFlash(_statueAnchor.GlobalPosition + Vector3.Up * 4.2f,
            new Color(1.0f, 0.55f, 0.15f), 1.6f);
        _crashAudio.Play();
        RefreshHud(force: true);
    }

    private void OnPartResisted(string partId, float energy)
    {
        if (energy > 18f)
        {
            _message = $"{partId} held firm ({energy:0} impact). Hit it harder!";
            _effects.SpawnChips(_statueAnchor.GlobalPosition + Vector3.Up * 3.6f, 0.45f, 2);
        }
        RefreshHud(force: true);
    }

    private void OnPartLostSupport(string partId)
    {
        _message = $"{partId} lost its support and fell intact.";
        RefreshHud(force: true);
    }

    private void OnStatueCollapsed()
    {
        if (_freeSmash)
            _message = "TOTAL COLLAPSE! Free Smash keeps going.";
        else
            _message = "The statue collapsed! R retries instantly.";
        _shake = Mathf.Max(_shake, 0.55f);
        _effects.SpawnDust(_statueAnchor.GlobalPosition + Vector3.Up * 2.4f, 1.35f, 3);
        _effects.SpawnChips(_statueAnchor.GlobalPosition + Vector3.Up * 2.4f, 1.2f, 7);
        Vector3 collapsePosition = _statueAnchor.GlobalPosition + Vector3.Up * 2.6f +
                                    Vector3.Back * 1.0f;
        _effects.SpawnSparks(collapsePosition, new Color(1.0f, 0.78f, 0.35f), 6);
        _effects.SpawnFlash(collapsePosition, new Color(1.0f, 0.62f, 0.18f), 5.2f);
        _rewardAudio.Play();
        RefreshHud(force: true);
    }

    private void OnStatueLeaned()
    {
        _message = "One support gone — the statue is leaning!";
        _shake = Mathf.Max(_shake, 0.25f);
        RefreshHud(force: true);
    }

    private void OnHardImpact(VehicleController vehicle, Vector3 position, float speed)
    {
        if (speed < 13f)
            return;
        float strength = Mathf.Clamp(speed / 22f, 0.55f, 1.5f);
        _effects.SpawnDust(position, strength * 0.7f, 1);
        _shake = Mathf.Max(_shake, 0.16f);
        _crashAudio.PitchScale = (float)GD.RandRange(0.7, 0.95);
        _crashAudio.VolumeDb = -12f;
        _crashAudio.Play();
    }

    private void OnBecameAirborne(VehicleController vehicle)
    {
        _launchAudio.Play();
        _message = "AIRBORNE! Aim for a weak point.";
        _shake = Mathf.Max(_shake, 0.16f);
    }

    private void OnLanded(VehicleController vehicle)
    {
        if (vehicle.PreSolveVelocity.Length() > 10f)
            _effects.SpawnDust(vehicle.GlobalPosition, 1.1f, 3);
    }

    private void RefreshHud(bool force)
    {
        if (_profile == null || _statue == null)
            return;

        WreckChallenge challenge = ChallengeCatalog.All[_challengeIndex];
        if (_freeSmash)
        {
            _modeLabel.Text = "FREE SMASH";
            _challengeLabel.Text = $"Target: {StatueCatalog.Names[_statueIndex]}";
            _goalLabel.Text = "Smash everything. No rewards, no pressure. J/K changes statue.";
            _requiredLabel.Text = $"PARTS BROKEN   {_statue.BrokenCount}";
            _requiredBar.MaxValue = Mathf.Max(1, _statue.PartCount);
            _requiredBar.Value = _statue.BrokenCount;
            _requiredBar.Visible = true;
            _totalLabel.Visible = false;
            _totalBar.Visible = false;
        }
        else
        {
            _modeLabel.Text = $"CAMPAIGN  {_challengeIndex + 1}/9";
            _challengeLabel.Text = $"{challenge.Name}  —  {challenge.StatueName}";
            _goalLabel.Text = challenge.Goal;
            ObjectiveProgress progress = _statue.GetObjectiveProgress(challenge);
            if (progress.RequiredTotal > 0)
            {
                _requiredLabel.Text = progress.RequiredLabel;
                _requiredBar.MaxValue = Mathf.Max(1, progress.RequiredTotal);
                _requiredBar.Value = progress.RequiredBroken;
                _requiredBar.Visible = true;
                _totalLabel.Visible = challenge.MinimumBrokenParts > challenge.RequiredParts.Length;
                _totalBar.Visible = _totalLabel.Visible;
                _totalLabel.Text = progress.TotalLabel;
                _totalBar.MaxValue = Mathf.Max(1, challenge.MinimumBrokenParts);
                _totalBar.Value = progress.TotalBroken;
            }
            else
            {
                _requiredLabel.Text = progress.TotalLabel;
                _requiredBar.MaxValue = Mathf.Max(1, challenge.MinimumBrokenParts);
                _requiredBar.Value = progress.TotalBroken;
                _requiredBar.Visible = true;
                _totalLabel.Visible = false;
                _totalBar.Visible = false;
            }
        }

        int best = _profile.Data.BestMedals[_challengeIndex];
        string medal = ChallengeCatalog.MedalLabel((Medal)best);
        string stars = best switch
        {
            3 => "★★★",
            2 => "★★☆",
            1 => "★☆☆",
            _ => "☆☆☆",
        };
        _statsLabel.Text =
            $"{_profile.Data.Coins} coins   ·   {stars} {medal}   ·   Ramp {_rampDegrees:0}°";
        _statusLabel.Text = _message;

        if (_vehicle != null)
        {
            _speedLabel.Text = $"{_vehicle.SpeedKph:0}";
            _speedBar.Value = Mathf.Clamp(_vehicle.SpeedKph, 0f, 200f);
            string gear = _vehicle.IsGrounded
                ? (_vehicle.ForwardSpeed < -0.4f ? "R" : "D")
                : "AIR";
            if (Input.IsActionPressed("brake") && _vehicle.IsGrounded)
                gear = "BRAKE";
            _gearLabel.Text = $"{gear}   ·   {Mathf.Abs(_vehicle.ForwardSpeed):0} M/S" +
                              (_vehicle.IsGrounded ? "" : "   ·   FLYING");
        }
        _garageCoins.Text = $"Wreck Coins: {_profile.Data.Coins}";

        if (force)
            RefreshGarage();
    }

    private void RefreshGarage()
    {
        if (_profile == null)
            return;
        bool[] unlocked = _profile.Data.VehiclesUnlocked;
        _rallyButton.Text = unlocked[0]
            ? (_vehicleIndex == 0 ? "Rally Car  ·  SELECTED" : "Rally Car")
            : "Rally Car  ·  LOCKED";
        _pickupButton.Text = unlocked[1]
            ? (_vehicleIndex == 1 ? "Pickup Truck  ·  SELECTED" : "Pickup Truck")
            : "Pickup Truck  ·  LOCKED";
        _monsterButton.Text = unlocked[2]
            ? (_vehicleIndex == 2 ? "Monster Truck  ·  SELECTED" : "Monster Truck")
            : $"Monster Truck  ·  BUY {GameProfile.MonsterTruckCost}";
        _engineButton.Text = _profile.Data.EngineLevel >= 1
            ? "Engine  ·  OWNED"
            : $"Engine upgrade  ·  {GameProfile.EngineUpgradeCost}";
        _gripButton.Text = _profile.Data.GripLevel >= 1
            ? "Tyres  ·  OWNED"
            : $"Tyre upgrade  ·  {GameProfile.GripUpgradeCost}";
        _paintButton.Text = _profile.Data.PaintUnlocked
            ? $"Paint  ·  {VehicleController.PaintName(_profile.Data.PaintIndex)}"
            : $"Unlock paint  ·  {GameProfile.PaintCost}";
        var selectedColor = new Color(0.13f, 0.86f, 0.93f);
        var normalColor = new Color(0.82f, 0.92f, 0.96f);
        _rallyButton.AddThemeColorOverride("font_color", _vehicleIndex == 0 ? selectedColor : normalColor);
        _pickupButton.AddThemeColorOverride("font_color", _vehicleIndex == 1 ? selectedColor : normalColor);
        _monsterButton.AddThemeColorOverride("font_color", _vehicleIndex == 2 ? selectedColor : normalColor);
        _engineButton.Disabled = _profile.Data.EngineLevel >= 1;
        _gripButton.Disabled = _profile.Data.GripLevel >= 1;
        _paintButton.Disabled = _profile.Data.PaintUnlocked;
        _qualityButton.Text = _profile.Data.QualityPreset == 0
            ? "Quality  ·  HIGH"
            : "Quality  ·  PERFORMANCE";
        _volumeButton.Text = $"Volume  ·  {VolumePercent()}%";
        _garageCoins.Text = $"WRECK COINS   {_profile.Data.Coins}";
        _garageHint.Text =
            $"This run: {_runScore} score  ·  {_runTime:0.0}s  ·  ramp {_rampDegrees:0}°.  " +
            "Use the mouse or 1–3 to choose a vehicle. Free Smash never grants coins.";
    }

    private void ConnectGarageButtons()
    {
        _rallyButton.Pressed += () => { SelectVehicle(0); ToggleGarage(false); };
        _pickupButton.Pressed += () => { SelectVehicle(1); ToggleGarage(false); };
        _monsterButton.Pressed += OnMonsterButton;
        _engineButton.Pressed += OnEngineButton;
        _gripButton.Pressed += OnGripButton;
        _paintButton.Pressed += OnPaintButton;
        _garage.GetNode<Button>("GarageVBox/ModeRow/CampaignButton").Pressed += () =>
        {
            if (_freeSmash)
                ToggleFreeSmash();
            ToggleGarage(false);
        };
        _garage.GetNode<Button>("GarageVBox/ModeRow/FreeSmashButton").Pressed += () =>
        {
            if (!_freeSmash)
                ToggleFreeSmash();
            ToggleGarage(false);
        };
        _qualityButton.Pressed += CycleQuality;
        _volumeButton.Pressed += CycleVolume;
        _helpClose.Pressed += () => ToggleHelp(false);
        _garage.GetNode<Button>("GarageVBox/CloseButton").Pressed += () => ToggleGarage(false);
    }

    private void OnMonsterButton()
    {
        if (_profile.Data.VehiclesUnlocked[2])
        {
            SelectVehicle(2);
            ToggleGarage(false);
            return;
        }
        PurchaseResult result = _profile.TryUnlockMonster();
        _garageHint.Text = result.Message;
        if (result.Success)
        {
            bool saved = _profile.TrySave();
            _message = result.Message + (saved ? "" : " Save failed: " + _profile.LastSaveError);
            if (!saved)
                PushSaveBanner("Save failed: " + _profile.LastSaveError, sticky: true);
            SelectVehicle(2);
            ToggleGarage(false);
        }
        else
        {
            _message = result.Message;
        }
        RefreshHud(force: true);
    }

    private void OnEngineButton()
    {
        PurchaseResult result = _profile.TryUpgradeEngine();
        _garageHint.Text = result.Message;
        if (result.Success)
        {
            bool saved = _profile.TrySave();
            if (!saved)
                PushSaveBanner("Save failed: " + _profile.LastSaveError, sticky: true);
            _vehicle?.ApplyProfile(_profile.Data.EngineLevel, _profile.Data.GripLevel,
                _profile.Data.PaintIndex, _profile.Data.PaintUnlocked);
            _message = result.Message;
        }
        RefreshHud(force: true);
    }

    private void OnGripButton()
    {
        PurchaseResult result = _profile.TryUpgradeGrip();
        _garageHint.Text = result.Message;
        if (result.Success)
        {
            bool saved = _profile.TrySave();
            if (!saved)
                PushSaveBanner("Save failed: " + _profile.LastSaveError, sticky: true);
            _vehicle?.ApplyProfile(_profile.Data.EngineLevel, _profile.Data.GripLevel,
                _profile.Data.PaintIndex, _profile.Data.PaintUnlocked);
            _message = result.Message;
        }
        RefreshHud(force: true);
    }

    private void OnPaintButton()
    {
        PurchaseResult result = _profile.TryUnlockPaint();
        _garageHint.Text = result.Message;
        if (result.Success)
        {
            bool saved = _profile.TrySave();
            if (!saved)
                PushSaveBanner("Save failed: " + _profile.LastSaveError, sticky: true);
            _vehicle?.ApplyProfile(_profile.Data.EngineLevel, _profile.Data.GripLevel,
                _profile.Data.PaintIndex, _profile.Data.PaintUnlocked);
            _message = result.Message;
        }
        RefreshHud(force: true);
    }

    private void CycleQuality()
    {
        _profile.Data.QualityPreset = _profile.Data.QualityPreset == 0 ? 1 : 0;
        ApplySettings();
        _profile.TrySave();
        _message = _profile.Data.QualityPreset == 0
            ? "Quality: High (shadows and fog on)"
            : "Quality: Performance (shadows and fog off)";
        RefreshHud(force: true);
    }

    private void CycleVolume()
    {
        _profile.Data.MasterVolumeDb = _profile.Data.MasterVolumeDb switch
        {
            >= -0.1f => -6f,
            >= -6.1f => -12f,
            _ => 0f,
        };
        ApplySettings();
        _profile.TrySave();
        _message = $"Master volume: {VolumePercent()}%";
        RefreshHud(force: true);
    }

    private int VolumePercent() =>
        Mathf.RoundToInt(Mathf.Pow(10f, _profile.Data.MasterVolumeDb / 20f) * 100f);

    private void ApplySettings()
    {
        AudioServer.SetBusVolumeDb(0, _profile.Data.MasterVolumeDb);
        AudioServer.SetBusMute(0, false);
        bool high = _profile.Data.QualityPreset == 0;
        DirectionalLight3D? sun = GetNodeOrNull<DirectionalLight3D>("Sun");
        if (sun != null)
            sun.ShadowEnabled = high;
        WorldEnvironment? world = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
        if (world?.Environment != null)
            world.Environment.FogEnabled = high;
        if (IsInsideTree())
            GetViewport().Msaa3D = high ? Viewport.Msaa.Msaa4X : Viewport.Msaa.Disabled;
    }

    private void ToggleGarage() => ToggleGarage(!_garageOpen);

    private void ToggleGarage(bool open)
    {
        if (open && _helpOpen)
            ToggleHelp(false);
        _garageOpen = open;
        _garage.Visible = open;
        _modalBackdrop.Visible = open || _helpOpen;
        if (_vehicle != null)
            _vehicle.ControlsEnabled = !(open || _helpOpen);
        if (open)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _orbiting = false;
            RefreshGarage();
        }
    }

    private void ToggleHelp() => ToggleHelp(!_helpOpen);

    private void ToggleHelp(bool open)
    {
        _helpOpen = open;
        _helpPanel.Visible = open;
        _modalBackdrop.Visible = open || _garageOpen;
        if (open)
        {
            if (_garageOpen)
                ToggleGarage(false);
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _orbiting = false;
        }
        if (_vehicle != null)
            _vehicle.ControlsEnabled = !(open || _garageOpen);
        if (open)
            _helpClose.GrabFocus();
    }

    private void ShowBanner(string text, float seconds)
    {
        _bannerLabel.Text = text;
        _bannerPanel.Visible = true;
        _bannerTimer = seconds;
    }

    private void PushSaveBanner(string text, bool sticky)
    {
        _saveBanner.Text = text;
        _saveBanner.Visible = true;
        _saveBannerSticky = sticky;
        _saveBannerTimer = sticky ? 0f : 5f;
    }

    private static string CameraName(int preset) => preset switch
    {
        0 => "Chase",
        1 => "Wide",
        _ => "Hood",
    };

    private void StopAudio()
    {
        foreach (AudioStreamPlayer player in new[] { _engineAudio, _crashAudio, _rewardAudio, _launchAudio })
        {
            if (player == null || !GodotObject.IsInstanceValid(player))
                continue;
            player.Stop();
            player.Stream = null;
        }
    }

    private void ConfigureEngineAudio()
    {
        if (_engineAudio.Stream is AudioStreamWav wav)
        {
            // Configure the imported resource in place. Duplicating it left an
            // AudioStreamWAV/playback pair alive when the dummy audio driver
            // shut down, even though the player had been stopped and released.
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = 0;
            wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
        }
        if (_audioAvailable)
            _engineAudio.Play();
    }

    private static void ConfigureInput()
    {
        Bind("drive_forward", Key.W, Key.Up);
        Bind("drive_reverse", Key.S, Key.Down);
        Bind("steer_left", Key.A, Key.Left);
        Bind("steer_right", Key.D, Key.Right);
        Bind("brake", Key.Space);
        Bind("retry", Key.R);
        Bind("vehicle_one", Key.Key1);
        Bind("vehicle_two", Key.Key2);
        Bind("vehicle_three", Key.Key3);
        Bind("previous_challenge", Key.J);
        Bind("next_challenge", Key.K);
        Bind("ramp_down", Key.Q);
        Bind("ramp_up", Key.E);
        Bind("free_smash", Key.F);
        Bind("camera_toggle", Key.C);
        Bind("garage", Key.G, Key.Tab);
        Bind("paint", Key.P);
        Bind("help", Key.H);
        Bind("pause", Key.Escape);
        Bind("quality_toggle", Key.B);
        Bind("volume_toggle", Key.V);

        BindAxis("drive_forward", JoyAxis.LeftY, -1f);
        BindAxis("drive_reverse", JoyAxis.LeftY, 1f);
        BindAxis("steer_left", JoyAxis.LeftX, -1f);
        BindAxis("steer_right", JoyAxis.LeftX, 1f);
        BindButton("brake", JoyButton.A);
        BindButton("retry", JoyButton.Y);
        BindButton("camera_toggle", JoyButton.RightShoulder);
        BindButton("garage", JoyButton.Start);
        BindButton("free_smash", JoyButton.Back);
        BindButton("vehicle_one", JoyButton.X);
        BindButton("vehicle_two", JoyButton.B);
        BindButton("vehicle_three", JoyButton.LeftShoulder);
        BindButton("previous_challenge", JoyButton.DpadLeft);
        BindButton("next_challenge", JoyButton.DpadRight);
        BindButton("ramp_down", JoyButton.DpadDown);
        BindButton("ramp_up", JoyButton.DpadUp);
    }

    private static void BindButton(string name, JoyButton button)
    {
        if (!InputMap.HasAction(name))
            InputMap.AddAction(name);
        foreach (InputEvent existing in InputMap.ActionGetEvents(name))
            if (existing is InputEventJoypadButton joy && joy.ButtonIndex == button)
                return;
        InputMap.ActionAddEvent(name, new InputEventJoypadButton { ButtonIndex = button });
    }

    private static void BindAxis(string name, JoyAxis axis, float value)
    {
        if (!InputMap.HasAction(name))
            InputMap.AddAction(name);
        foreach (InputEvent existing in InputMap.ActionGetEvents(name))
            if (existing is InputEventJoypadMotion motion &&
                motion.Axis == axis && Mathf.IsEqualApprox(motion.AxisValue, value))
                return;
        InputMap.ActionAddEvent(name, new InputEventJoypadMotion
        {
            Axis = axis,
            AxisValue = value,
        });
    }

    private static void Bind(string name, params Key[] keys)
    {
        if (!InputMap.HasAction(name))
            InputMap.AddAction(name);
        foreach (Key key in keys)
        {
            bool present = false;
            foreach (InputEvent existing in InputMap.ActionGetEvents(name))
            {
                if (existing is InputEventKey keyEvent &&
                    keyEvent.PhysicalKeycode == key)
                {
                    present = true;
                    break;
                }
            }
            if (!present)
                InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
        }
    }

    // ---- deterministic test hooks -------------------------------------------------
    public void TestSelectVehicle(int index) => SelectVehicle(index);
    public void TestSelectChallenge(int index)
    {
        _challengeIndex = Mathf.Clamp(index, 0, ChallengeCatalog.All.Length - 1);
        _rampDegrees = ChallengeCatalog.All[_challengeIndex].RecommendedRampDegrees;
        UpdateRamp();
        ResetRun(true, true, true);
    }
    public void TestSetRampDegrees(float degrees)
    {
        _rampDegrees = Mathf.Clamp(degrees, 10f, 27f);
        UpdateRamp();
    }
    public float TestRampDegrees => _rampDegrees;
    public StatueController? TestSpawnStatueIndex(int index)
    {
        _statueIndex = Mathf.Clamp(index, 0, StatueScenes.Length - 1);
        _freeSmash = true;
        SpawnStatue();
        return _statue;
    }
    public int TestRunScore => _runScore;
    public float TestRunTime => _runTime;
    public bool TestChallengeCompleted => _challengeCompletedThisRun;
    public string TestStatus => _message;
    public void TestRetry() => ResetRun(true, true, true);
    public Camera3D TestCamera => _camera;
    public void TestShowcaseStatue()
    {
        _showcaseCamera = true;
        GetNode<CanvasLayer>("Hud").Visible = false;
        Vector3 focus = _statueAnchor.GlobalPosition + Vector3.Up * 3.2f;
        _camera.GlobalPosition = focus + new Vector3(6.0f, 3.8f, 10.0f);
        _camera.LookAt(focus, Vector3.Up);
        _camera.Fov = 42f;
    }
    public float TestCinematicTimer => _cinematicTimer;
    public ImpactEffects TestEffects => _effects;
    public string TestProgressText => _requiredLabel.Text;
    public string TestTotalProgressText => _totalLabel.Text;
    public void TestSetGarage(bool open) => ToggleGarage(open);
    public bool TestGarageOpen => _garageOpen;
    public bool TestHelpOpen => _helpOpen;
    public void TestSetHelp(bool open) => ToggleHelp(open);
    public void TestRefreshHud() => RefreshHud(force: true);
    public void TestSetQuality(int preset)
    {
        _profile.Data.QualityPreset = Mathf.Clamp(preset, 0, 1);
        ApplySettings();
    }
    public void TestStopControls()
    {
        if (_vehicle != null)
            _vehicle.ControlsEnabled = false;
    }
}
