using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace RampWreckers.Tests;

/// <summary>
/// Deterministic runtime acceptance harness. Run with:
///   godot --headless --path . res://tests/RuntimeTests.tscn
/// Exit code 0 means every check passed.
/// </summary>
public partial class RuntimeTests : Node
{
    private int _passed;
    private int _failed;
    private readonly List<string> _failures = new();
    private string _testRoot = string.Empty;

    public override void _Ready()
    {
        _testRoot = ProjectSettings.GlobalizePath("user://rw_runtime_tests");
        TryDeleteDirectory(_testRoot);
        Directory.CreateDirectory(_testRoot);
        OS.SetEnvironment("RW_SAVE_PATH", Path.Combine(_testRoot, "main-game-save.json"));
        Callable.From(() => { _ = RunAll(); }).CallDeferred();
    }

    private async Task RunAll()
    {
        GD.Print("=== RAMP WRECKERS RUNTIME TESTS ===");
        try
        {
            TestCatalog();
            TestImpactModel();
            TestAudioAssets();
            await TestStatueGraphs();
            await TestChallengeFeasibility();
            await TestStatueResetCycle();
            TestSaveSystem();
            await TestRampGeometry();
            await TestInputSmoke();
            await TestEndToEndDrive();
            await TestRetryBounds();
        }
        catch (Exception ex)
        {
            Fail("Unhandled test exception: " + ex);
        }

        GD.Print($"=== RESULT: {_passed} passed, {_failed} failed ===");
        foreach (string failure in _failures)
            GD.Print("FAILED: " + failure);
        GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    private void Check(bool condition, string description)
    {
        if (condition)
        {
            _passed++;
            GD.Print("[PASS] " + description);
        }
        else
        {
            Fail(description);
        }
    }

    private void Fail(string description)
    {
        _failed++;
        _failures.Add(description);
        GD.Print("[FAIL] " + description);
    }

    private async Task WaitPhysics(int frames)
    {
        for (int i = 0; i < frames; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task<T> SpawnStatue<T>(string scenePath, Vector3 position) where T : Node3D
    {
        var holder = new Node3D { Name = "TestHolder" };
        AddChild(holder);
        holder.Position = position;
        T statue = GD.Load<PackedScene>(scenePath).Instantiate<T>();
        holder.AddChild(statue);
        await WaitPhysics(2);
        return statue;
    }

    private void TestCatalog()
    {
        Check(ChallengeCatalog.All.Length == 9, "catalog has nine authored challenges");
        Check(ChallengeCatalog.TryValidate(out List<string> errors),
            "catalog part IDs validate against each statue (" + string.Join("; ", errors) + ")");
        Check(ChallengeCatalog.All.All(c => c.Reward > 0), "every challenge awards positive coins");
        Check(ChallengeCatalog.IsComplete(ChallengeCatalog.All[1],
                new HashSet<string> { "Hat" }), "Hat Trick completes with Hat");
        Check(!ChallengeCatalog.IsComplete(ChallengeCatalog.All[4],
                new HashSet<string> { "LeftLeg" }), "Knee Trouble rejects one leg");
        Medal medal = ChallengeCatalog.MedalFor(ChallengeCatalog.All[0],
            new HashSet<string> { "LeftLeg", "RightLeg" }, 1000, 12f);
        Check(medal == Medal.Gold, "medal calc returns Gold for high score and fast time");
    }

    private void TestAudioAssets()
    {
        foreach (string path in new[]
                 {
                     "res://art/audio/crash.wav", "res://art/audio/launch.wav",
                     "res://art/audio/reward.wav", "res://art/audio/engine_loop.wav",
                 })
        {
            AudioStream? stream = GD.Load<AudioStream>(path);
            Check(stream != null && stream.GetLength() > 0.15f,
                $"audio cue loads with usable length: {path}");
        }
    }

    private void TestImpactModel()
    {
        Check(ImpactModel.Energy(0, 10) == 0f, "zero mass gives zero impact energy");
        Check(ImpactModel.Energy(1500, 20) > ImpactModel.Energy(1500, 10),
            "impact energy grows with speed");
        Check(!ImpactModel.Breaks(50, 90), "below threshold does not break");
        Check(ImpactModel.Breaks(90, 90), "at threshold breaks exactly");
        Check(ImpactModel.Score(500, 50, false) > ImpactModel.Score(100, 50, false),
            "higher energy awards more score");
        Check(ImpactModel.Score(500, 50, true) > ImpactModel.Score(500, 50, false),
            "weak point score is boosted");
    }

    private async Task TestStatueGraphs()
    {
        StatueController gnome = await SpawnStatue<StatueController>("res://scenes/statues/Gnome.tscn", new Vector3(0, 0, 0));
        Check(gnome.PartCount == 8, "gnome has eight authored breakable parts");
        Check(gnome.DebugGraphValid, "gnome structural graph is valid");
        ImpactResult tooSlow = gnome.ResolveImpact("Torso", 500f, 2f, Vector3.Back);
        Check(tooSlow == ImpactResult.Ignored && gnome.BrokenCount == 0,
            "low-speed statue contact awards no damage");
        ImpactResult resisted = gnome.ResolveImpact("Torso", 10f, 10f, Vector3.Back);
        Check(resisted == ImpactResult.Resisted && gnome.BrokenCount == 0,
            "low-energy statue hit is resisted");
        ImpactResult first = gnome.ResolveImpact("LeftLeg", 500f, 20f, Vector3.Back);
        Check(first is ImpactResult.Broken or ImpactResult.Collapsed &&
              gnome.BrokenPartIds.Contains("LeftLeg"), "strong leg hit breaks the leg");
        int afterFirst = gnome.BrokenCount;
        ImpactResult duplicate = gnome.ResolveImpact("LeftLeg", 500f, 20f, Vector3.Back);
        Check(duplicate == ImpactResult.Ignored && gnome.BrokenCount == afterFirst,
            "duplicate hit cannot double-count a part");
        Transform3D leanedTorso = gnome.GetPart("Torso")!.Transform;
        Check(!leanedTorso.IsEqualApprox(gnome.GetPart("Torso")!.InitialLocalTransform),
            "one broken support visibly leans the statue");
        gnome.ResolveImpact("RightLeg", 500f, 20f, Vector3.Back);
        Check(gnome.BrokenPartIds.Contains("LeftLeg") && gnome.BrokenPartIds.Contains("RightLeg"),
            "both gnome supports can be broken");
        Check(gnome.BrokenPartIds.Contains("Torso"),
            "both supports gone topples the gnome torso");
        Check(gnome.BrokenCount >= 4, "support collapse breaks the upper structure");
        Check(gnome.IsCollapsed, "collapse event state is set");
        gnome.ResetStatue();
        bool restored = gnome.SnapshotParts().All(p =>
            !p.IsBroken && p.Transform.IsEqualApprox(p.InitialLocalTransform));
        Check(restored && gnome.BrokenCount == 0, "statue reset restores every authored pose");
        gnome.GetParent().QueueFree();
        await WaitPhysics(1);

        StatueController knight = await SpawnStatue<StatueController>("res://scenes/statues/Knight.tscn", new Vector3(20, 0, 0));
        Check(knight.PartCount == 7 && knight.DebugGraphValid, "knight has seven valid parts");
        knight.ResolveImpact("Shield", 400f, 18f, Vector3.Back);
        Check(knight.BrokenPartIds.Contains("Shield"), "shield detaches from a direct hit");
        knight.ResetStatue();
        knight.ResolveImpact("LeftLeg", 500f, 20f, Vector3.Back);
        knight.ResolveImpact("RightLeg", 500f, 20f, Vector3.Back);
        Check(knight.BrokenPartIds.Contains("Torso"),
            "knight torso collapses after both legs");
        knight.GetParent().QueueFree();
        await WaitPhysics(1);

        StatueController robot = await SpawnStatue<StatueController>("res://scenes/statues/Robot.tscn", new Vector3(40, 0, 0));
        Check(robot.PartCount == 8 && robot.DebugGraphValid, "robot has eight valid parts");
        ImpactResult blocked = robot.ResolveImpact("Core", 500f, 20f, Vector3.Back);
        StatuePart core = robot.GetPart("Core")!;
        StatuePart armour = robot.GetPart("Armour")!;
        Check(blocked == ImpactResult.Blocked && !core.IsBroken,
            "robot armour blocks a direct core strike");
        Check(armour.IsBroken, "blocked core strike transfers damage to armour");
        ImpactResult exposed = robot.ResolveImpact("Core", 500f, 20f, Vector3.Back);
        Check(exposed is ImpactResult.Broken or ImpactResult.Collapsed && core.IsBroken,
            "exposed robot core breaks on a direct weak-point strike");
        robot.ResetStatue();
        robot.ResolveImpact("LeftLeg", 600f, 22f, Vector3.Back);
        robot.ResolveImpact("RightLeg", 600f, 22f, Vector3.Back);
        StatuePart detachedCore = robot.GetPart("Core")!;
        Check(!detachedCore.IsBroken && detachedCore.IsLoose,
            "robot core detaches intact when its torso collapses");
        Check(robot.BrokenPartIds.Contains("Torso") && robot.BrokenPartIds.Contains("Armour"),
            "robot torso and armour break in the collapse");
        robot.GetParent().QueueFree();
        await WaitPhysics(1);
    }

    private async Task TestChallengeFeasibility()
    {
        for (int i = 0; i < ChallengeCatalog.All.Length; i++)
        {
            WreckChallenge challenge = ChallengeCatalog.All[i];
            string scene = challenge.StatueIndex switch
            {
                0 => "res://scenes/statues/Gnome.tscn",
                1 => "res://scenes/statues/Knight.tscn",
                _ => "res://scenes/statues/Robot.tscn",
            };
            StatueController statue = await SpawnStatue<StatueController>(scene,
                new Vector3(0, 0, 200f + i * 12f));
            foreach (string part in challenge.RequiredParts)
            {
                // Robot core has to be exposed first; a blocked core hit chips
                // armour, exactly as in gameplay.
                if (part == "Core")
                {
                    statue.ResolveImpact("Armour", 420f, 20f, Vector3.Back);
                    statue.ResolveImpact("Core", 420f, 20f, Vector3.Back);
                }
                else
                {
                    statue.ResolveImpact(part, 420f, 20f, Vector3.Back);
                }
            }
            foreach (string part in StatueCatalog.PartIds[challenge.StatueIndex])
            {
                if (statue.BrokenCount >= challenge.MinimumBrokenParts)
                    break;
                statue.ResolveImpact(part, 420f, 20f, Vector3.Back);
            }
            Check(ChallengeCatalog.IsComplete(challenge, statue.BrokenPartIds),
                $"challenge {i + 1} '{challenge.Name}' is completable from real part breaks");
            statue.GetParent().QueueFree();
            await WaitPhysics(1);
        }
    }

    private async Task TestStatueResetCycle()
    {
        StatueController statue = await SpawnStatue<StatueController>("res://scenes/statues/Robot.tscn", new Vector3(-30, 0, 0));
        Dictionary<string, Transform3D> initial = statue.SnapshotParts()
            .ToDictionary(p => p.PartId, p => p.Transform);
        bool allEqual = true;
        for (int cycle = 0; cycle < 30; cycle++)
        {
            statue.ResolveImpact("Armour", 500f, 20f, Vector3.Back);
            statue.ResolveImpact("LeftArm", 500f, 20f, Vector3.Back);
            statue.ResolveImpact("RightArm", 500f, 20f, Vector3.Back);
            statue.ResetStatue();
            if (statue.BrokenCount != 0 || statue.LoosePartIds.Count != 0)
                allEqual = false;
            foreach (StatuePart part in statue.SnapshotParts())
            {
                if (!part.Transform.IsEqualApprox(initial[part.PartId]))
                    allEqual = false;
                if (!part.GetParent().IsQueuedForDeletion() && part.IsBroken)
                    allEqual = false;
            }
        }
        Check(allEqual, "30 break/reset cycles restore exact part transforms and state");
        statue.GetParent().QueueFree();
        await WaitPhysics(1);
    }

    private void TestSaveSystem()
    {
        string dir = Path.Combine(_testRoot, "save");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "profile.json");
        var store = new SaveStore(path);
        SaveData data = SaveData.CreateDefault();
        data.Coins = 123;
        data.BestMedals[0] = 3;
        data.RewardClaimed[0] = true;
        data.VehiclesUnlocked[2] = true;
        Check(store.Save(data), "save writes a valid payload");
        SaveLoadResult loaded = store.Load();
        Check(loaded.LoadedFromDisk && loaded.Data.Coins == 123 &&
              loaded.Data.BestMedals[0] == 3 && loaded.Data.VehiclesUnlocked[2],
            "save roundtrip restores coins, medals and unlocks");

        SaveData second = SaveData.CreateDefault();
        second.Coins = 999;
        Check(store.Save(second), "second save writes and creates a backup");
        File.WriteAllText(path, "{ this is not valid json");
        SaveLoadResult recovered = store.Load();
        Check(recovered.RecoveredFromBackup && recovered.Data.Coins == 123,
            "corrupt primary save recovers the last good backup");

        File.WriteAllText(path, string.Empty);
        TryDeleteFile(store.BackupPath);
        SaveLoadResult empty = store.Load();
        Check(empty.HadProblem && empty.Data.Coins == 0,
            "empty save recovers to a safe default profile");

        string validBefore = System.Text.Json.JsonSerializer.Serialize(loaded.Data);
        SaveData invalid = SaveData.CreateDefault();
        invalid.Format = "not-ours";
        Check(!store.Save(invalid) && !string.IsNullOrEmpty(store.LastError),
            "invalid save data is refused with honest feedback");

        string blocker = Path.Combine(dir, "blocker");
        File.WriteAllText(blocker, "not a directory");
        var brokenStore = new SaveStore(Path.Combine(blocker, "profile.json"));
        Check(!brokenStore.Save(SaveData.CreateDefault()) &&
              brokenStore.LastError.Contains("Could not save", StringComparison.OrdinalIgnoreCase),
            "write failure is reported, not claimed as success");

        var profile = new GameProfile(store);
        profile.Data.Coins = 5000;
        RewardResult first = profile.RecordChallenge(0, Medal.Bronze);
        RewardResult replay = profile.RecordChallenge(0, Medal.Gold);
        Check(first.FirstClear && first.Coins == ChallengeCatalog.All[0].Reward,
            "first challenge clear awards coins once");
        Check(!replay.FirstClear && replay.Coins == 0 &&
              profile.Data.BestMedals[0] == (int)Medal.Gold,
            "challenge replay improves medal but cannot duplicate coins");
        Check(profile.Data.Coins == 5000 + ChallengeCatalog.All[0].Reward,
            "profile coins reflect the single reward exactly");
    }

    private async Task TestRampGeometry()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(3);
        CollisionShape3D collision = main.GetNode<CollisionShape3D>("RampPivot/Collision");
        var box = (BoxShape3D)collision.Shape;
        foreach (float angle in new[] { 10f, 16f, 27f })
        {
            main.TestSetRampDegrees(angle);
            await WaitPhysics(1);
            Transform3D transform = collision.GlobalTransform;
            Vector3 nearTop = transform * new Vector3(0, box.Size.Y * 0.5f, box.Size.Z * 0.5f);
            Vector3 farTop = transform * new Vector3(0, box.Size.Y * 0.5f, -box.Size.Z * 0.5f);
            Check(nearTop.Y > -0.25f && nearTop.Y < 0.30f,
                $"ramp {angle}° foot is flush with the runway (y={nearTop.Y:0.00})");
            Check(farTop.Y > 1.6f,
                $"ramp {angle}° lifts the launch lip off the ground (y={farTop.Y:0.00})");
            float expectedRise = box.Size.Z * Mathf.Sin(Mathf.DegToRad(angle));
            Check(Mathf.Abs((farTop.Y - nearTop.Y) - expectedRise) < 0.35f,
                $"ramp {angle}° collider slope matches the authored deck");
        }
        main.QueueFree();
        await WaitPhysics(1);
    }

    private async Task TestInputSmoke()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);

        await TapKey(Key.G);
        Check(main.TestGarageOpen, "G key opens the garage overlay");
        await TapKey(Key.G);
        Check(!main.TestGarageOpen, "G key closes the garage overlay");
        await TapKey(Key.F);
        Check(main.IsFreeSmash, "F key enables Free Smash");
        int coinsBefore = main.Profile.Data.Coins;
        bool rewardBefore = main.Profile.Data.RewardClaimed[0];
        StatueController? smashStatue = main.CurrentStatue;
        smashStatue?.ResolveImpact("LeftLeg", 500f, 20f, Vector3.Back);
        smashStatue?.ResolveImpact("RightLeg", 500f, 20f, Vector3.Back);
        await WaitPhysics(4);
        Check(main.Profile.Data.Coins == coinsBefore &&
              main.Profile.Data.RewardClaimed[0] == rewardBefore,
            "Free Smash destruction cannot grant campaign coins or corrupt rewards");
        await TapKey(Key.F);
        Check(!main.IsFreeSmash, "F key returns to Campaign");
        int before = main.Profile.Data.CameraPreset;
        await TapKey(Key.C);
        Check(main.Profile.Data.CameraPreset != before, "C key cycles the camera preset");

        main.QueueFree();
        await WaitPhysics(1);
    }

    private async Task TapKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key,
            Pressed = true,
        });
        await WaitPhysics(2);
        Input.ParseInputEvent(new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key,
            Pressed = false,
        });
        await WaitPhysics(2);
    }

    private async Task TestEndToEndDrive()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        main.TestSelectChallenge(0);
        await WaitPhysics(6);

        main.Profile.Data.VehiclesUnlocked[2] = true;
        for (int vehicle = 0; vehicle < 3; vehicle++)
        {
            main.TestSelectVehicle(vehicle);
            main.TestSetRampDegrees(16f);
            await WaitPhysics(15);
            VehicleController? car = main.CurrentVehicle;
            Check(car != null && car.VehicleIndex == vehicle,
                $"vehicle {vehicle} spawned for route test");
            if (car == null)
                continue;

            float startZ = car.GlobalPosition.Z;
            float minZ = startZ;
            float maxY = car.GlobalPosition.Y;
            float maxSpeed = 0f;
            int maxRunScore = 0;
            bool airborne = false;

            Input.ActionPress("drive_forward");
            float elapsed = 0f;
            for (int frame = 0; frame < 1500; frame++)
            {
                await WaitPhysics(1);
                elapsed += (float)GetPhysicsProcessDeltaTime();
                if (!GodotObject.IsInstanceValid(car))
                    break;
                Vector3 position = car.GlobalPosition;
                minZ = Mathf.Min(minZ, position.Z);
                maxY = Mathf.Max(maxY, position.Y);
                maxSpeed = Mathf.Max(maxSpeed, car.SpeedKph);
                maxRunScore = Mathf.Max(maxRunScore, main.TestRunScore);
                if (car.IsAirborne && position.Y > 1.5f)
                    airborne = true;
                if (maxRunScore > 0 && position.Z < -15f)
                    break;
                if (elapsed > 22f)
                    break;
            }
            Input.ActionRelease("drive_forward");
            await WaitPhysics(10);

            Check(startZ - minZ > 30f,
                $"vehicle {vehicle} physically travelled more than 30 m (z {startZ:0} -> {minZ:0})");
            Check(maxSpeed > 50f,
                $"vehicle {vehicle} reached a usable launch speed ({maxSpeed:0} km/h)");
            Check(airborne, $"vehicle {vehicle} became airborne from the ramp");
            Check(maxRunScore > 0,
                $"vehicle {vehicle} damaged the statue through ordinary driving (score {maxRunScore})");
            if (vehicle == 0)
                Check(main.Profile.Data.RewardClaimed[0] && main.Profile.Data.Coins >=
                      ChallengeCatalog.All[0].Reward,
                    "campaign route completes First Crash and saves the first-clear reward");
            GD.Print($"[ROUTE] vehicle={vehicle} z={startZ:0}->{minZ:0} " +
                     $"maxY={maxY:0.0} maxSpeed={maxSpeed:0} score={maxRunScore}");
            main.TestRetry();
            await WaitPhysics(8);
        }
        main.QueueFree();
        await WaitPhysics(1);
    }

    private async Task TestRetryBounds()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        bool stable = true;
        ulong start = Time.GetTicksMsec();
        for (int i = 0; i < 30; i++)
        {
            main.TestSelectChallenge(i % 9);
            main.TestSetRampDegrees(10f + (i % 9) * 2f);
            main.TestRetry();
            await WaitPhysics(2);
            if (main.CurrentStatue == null || main.CurrentVehicle == null ||
                main.CurrentStatue.BrokenCount != 0 ||
                main.CurrentStatue.PartCount < 7)
                stable = false;
        }
        ulong elapsed = Time.GetTicksMsec() - start;
        Check(stable, "30 retries keep statue/vehicle state bounded and intact");
        Check(elapsed < 2000, $"30 retries complete quickly ({elapsed} ms total)");
        main.QueueFree();
        await WaitPhysics(1);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort in a test harness.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort.
        }
    }
}
