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
            await TestProgressDisplay();
            await TestSolidCollisionPresentation();
            await TestContactAggregation();
            await TestCoreRequiresSecondContact();
            await TestEndToEndDrive();
            await TestPhysicalChallengeRoutes();
            await TestCameraAndEffects();
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
        Check(ChallengeCatalog.All.All(c => c.RecommendedRampDegrees is >= 10 and <= 27),
            "every challenge has an authored 10-27 degree suggested ramp");

        var unrelated = new HashSet<string> { "Torso", "Head", "LeftArm", "RightArm", "Hat" };
        ObjectiveProgress knee = ObjectiveProgressRules.Evaluate(ChallengeCatalog.All[4], unrelated);
        Check(knee.RequiredBroken == 0 && knee.RequiredTotal == 2 &&
              !knee.RequiredComplete && !knee.Complete &&
              knee.RequiredLabel.Contains("BOTH KNEES") && knee.RequiredLabel.Contains("0 / 2"),
            "Knee Trouble progress never counts unrelated broken pieces as knees");
        ObjectiveProgress core = ObjectiveProgressRules.Evaluate(ChallengeCatalog.All[7],
            new HashSet<string> { "Armour" });
        Check(core.RequiredBroken == 0 && !core.RequiredComplete && !core.Complete,
            "Core Shot remains incomplete while only the chest armour is broken");
        ObjectiveProgress hat = ObjectiveProgressRules.Evaluate(ChallengeCatalog.All[1],
            new HashSet<string> { "Hat" });
        Check(hat.RequiredBroken == 1 && hat.Complete, "Hat Trick progress completes on the hat");
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
        await RemoveNode(gnome.GetParent());

        StatueController knight = await SpawnStatue<StatueController>("res://scenes/statues/Knight.tscn", new Vector3(20, 0, 0));
        Check(knight.PartCount == 7 && knight.DebugGraphValid, "knight has seven valid parts");
        knight.ResolveImpact("Shield", 400f, 18f, Vector3.Back);
        Check(knight.BrokenPartIds.Contains("Shield"), "shield detaches from a direct hit");
        knight.ResetStatue();
        knight.ResolveImpact("LeftLeg", 500f, 20f, Vector3.Back);
        knight.ResolveImpact("RightLeg", 500f, 20f, Vector3.Back);
        Check(knight.BrokenPartIds.Contains("Torso"),
            "knight torso collapses after both legs");
        await RemoveNode(knight.GetParent());

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
        Check(robot.BrokenPartIds.Contains("Torso") && armour.IsLoose,
            "robot torso breaks and armour detaches intact in the collapse");
        await RemoveNode(robot.GetParent());
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
            await RemoveNode(statue.GetParent());
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
        await RemoveNode(statue.GetParent());
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

        string legacyPath = Path.Combine(dir, "legacy-v1.json");
        File.WriteAllText(legacyPath, """
{
  "format": "RampWreckersSave",
  "version": 1,
  "coins": 777,
  "vehiclesUnlocked": [true, true, false],
  "bestMedals": [1, 0, 0, 0, 0, 0, 0, 0, 0],
  "rewardClaimed": [true, false, false, false, false, false, false, false, false],
  "paintUnlocked": false,
  "paintIndex": 0,
  "engineLevel": 0,
  "gripLevel": 0,
  "cameraPreset": 0,
  "qualityPreset": 0,
  "masterVolumeDb": 0,
  "showHelp": true
}
""");
        SaveLoadResult legacy = new SaveStore(legacyPath).Load();
        Check(legacy.LoadedFromDisk && !legacy.HadProblem && legacy.Data.Coins == 777,
            "a v0.1.0 version-1 save file still loads unchanged");

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
        await RemoveNode(main);
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

        await RemoveNode(main);
    }

    private async Task TapKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key,
            Pressed = true,
        });
        await WaitPhysics(6);
        Input.ParseInputEvent(new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key,
            Pressed = false,
        });
        await WaitPhysics(6);
    }


    private async Task TestProgressDisplay()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        main.TestSelectChallenge(4);
        await WaitPhysics(8);
        StatueController? knee = main.CurrentStatue;
        Check(knee != null, "progress display test spawns the kneed statue");
        if (knee != null)
        {
            knee.ResolveImpact("Torso", 700f, 20f, Vector3.Back);
            knee.ResolveImpact("Hat", 700f, 20f, Vector3.Back);
            knee.ResolveImpact("Beard", 700f, 20f, Vector3.Back);
            main.TestRefreshHud();
            Check(main.TestProgressText.Contains("BOTH KNEES") &&
                  main.TestProgressText.Contains("0 / 2") &&
                  !main.TestProgressText.Contains("5 / 2"),
                "Knee Trouble HUD shows 0/2 required knees despite unrelated pieces breaking");
        }

        main.TestSelectChallenge(7);
        await WaitPhysics(8);
        StatueController? core = main.CurrentStatue;
        if (core != null)
        {
            core.ResolveImpact("Armour", 500f, 20f, Vector3.Back);
            main.TestRefreshHud();
            Check(main.TestProgressText.Contains("REACTOR CORE") &&
                  main.TestProgressText.Contains("0 / 1"),
                "Core Shot HUD stays at 0/1 while only armour is gone");
            core.ResolveImpact("Core", 500f, 20f, Vector3.Back);
            main.TestRefreshHud();
            Check(main.TestProgressText.Contains("1 / 1"),
                "Core Shot HUD reaches 1/1 only after the core breaks");
        }
        await RemoveNode(main);
    }


    private async Task TestSolidCollisionPresentation()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        main.TestSelectChallenge(4);
        await WaitPhysics(8);
        StatueController? statue = main.CurrentStatue;
        Check(statue != null, "collision presentation test spawns the knight");
        if (statue != null)
        {
            StatuePart? shield = statue.GetPart("Shield");
            StatuePart? leftLeg = statue.GetPart("LeftLeg");
            Check(shield != null && shield.CollisionLayer == 0,
                "knight shield is an ornament hit volume, not a false wall");
            Check(leftLeg != null && leftLeg.CollisionLayer == 1,
                "intact knight legs are solid world obstacles");
            statue.ResolveImpact("LeftLeg", 900f, 28f, Vector3.Back);
            await WaitPhysics(2);
            Check(leftLeg != null && leftLeg.CollisionLayer == 4,
                "broken leg leaves the world layer and becomes debris");
        }
        await RemoveNode(main);
    }


    private async Task TestContactAggregation()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        main.TestSelectChallenge(0);
        await WaitPhysics(8);
        StatueController? statue = main.CurrentStatue;
        VehicleController? car = main.CurrentVehicle;
        Check(statue != null && car != null, "contact aggregation test has a statue and vehicle");
        if (statue != null && car != null)
        {
            StatuePart? left = statue.GetPart("LeftLeg");
            StatuePart? right = statue.GetPart("RightLeg");
            Check(left != null && right != null, "contact aggregation test has both legs");
            if (left != null && right != null)
            {
                // Both reports happen in the same physics step. The second must
                // join the pending group instead of being rejected by cooldown.
                statue.ReportVehicleContact(left, car, 40000f, 30f,
                    Vector3.Back, left.GlobalPosition);
                statue.ReportVehicleContact(right, car, 40000f, 30f,
                    Vector3.Back, right.GlobalPosition);
                await WaitPhysics(4);
                Check(statue.BrokenPartIds.Contains("LeftLeg") &&
                      statue.BrokenPartIds.Contains("RightLeg"),
                    "same-step contacts retain every contacted part in the impact group");
            }
        }
        await RemoveNode(main);
    }


    private async Task TestCoreRequiresSecondContact()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        main.TestSelectChallenge(7);
        await WaitPhysics(8);
        StatueController? statue = main.CurrentStatue;
        VehicleController? car = main.CurrentVehicle;
        Check(statue != null && car != null, "core sequencing test has a statue and vehicle");
        if (statue != null && car != null)
        {
            StatuePart? armour = statue.GetPart("Armour");
            StatuePart? core = statue.GetPart("Core");
            Check(armour != null && core != null, "core sequencing test has armour and core");
            if (armour != null && core != null)
            {
                // The first group touches both volumes while the armour is still
                // intact: only the armour may peel.
                statue.ReportVehicleContact(armour, car, 40000f, 30f,
                    Vector3.Back, armour.GlobalPosition);
                statue.ReportVehicleContact(core, car, 40000f, 30f,
                    Vector3.Back, core.GlobalPosition);
                await WaitPhysics(40);
                Check(armour.IsBroken && !core.IsBroken,
                    "protected core contact cannot break the core in the armour group");
                // After the 550 ms cooldown, a fresh core contact is required.
                statue.ReportVehicleContact(core, car, 40000f, 30f,
                    Vector3.Back, core.GlobalPosition);
                await WaitPhysics(5);
                Check(core.IsBroken,
                    "a fresh contact after armour destruction breaks the exposed core");
            }
        }
        await RemoveNode(main);
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
        await RemoveNode(main);
    }


    private static readonly int[] RouteVehicles = { 1, 0, 1, 1, 0, 1, 2, 2, 0 };

    /// <summary>
    /// All nine objectives through ordinary driving only: spawn, throttle,
    /// ramp, real Area3D/impact contacts, reverse for a second pass where the
    /// objective needs it. No ResolveImpact, teleport or forced impulse.
    /// </summary>
    private async Task TestPhysicalChallengeRoutes()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        main.Profile.Data.VehiclesUnlocked[2] = true;

        for (int challengeIndex = 0; challengeIndex < ChallengeCatalog.All.Length; challengeIndex++)
        {
            WreckChallenge challenge = ChallengeCatalog.All[challengeIndex];
            main.TestSelectVehicle(RouteVehicles[challengeIndex]);
            main.TestSelectChallenge(challengeIndex);
            await WaitPhysics(18);
            VehicleController? car = main.CurrentVehicle;
            StatueController? statue = main.CurrentStatue;
            Check(car != null && statue != null,
                $"route {challengeIndex + 1} has a real vehicle and statue");
            if (car == null || statue == null)
                continue;

            main.TestSetRampDegrees(challenge.RecommendedRampDegrees);
            bool complete = false;
            int passes = 0;
            for (; passes < 5 && !complete; passes++)
            {
                complete = await DriveRoutePass(main, car, 9.5f);
                if (complete)
                    break;
                if (passes < 4)
                {
                    await ReverseRoutePass(car, 3.2f);
                    await WaitPhysics(24);
                }
            }
            await WaitPhysics(30);
            Check(complete, $"challenge {challengeIndex + 1} '{challenge.Name}' completes through real driving");
            Check(main.TestRunScore > 0,
                $"challenge {challengeIndex + 1} scored a real component impact");
            foreach (string required in challenge.RequiredParts)
                Check(statue.BrokenPartIds.Contains(required),
                    $"challenge {challengeIndex + 1} broke required component {required}");
            if (challenge.Name == "Core Shot")
                Check(statue.BrokenPartIds.Contains("Armour") &&
                      statue.BrokenPartIds.Contains("Core"),
                    "Core Shot route peeled armour first and then broke the exposed core");
            GD.Print($"[ROUTE] challenge={challengeIndex + 1} {challenge.Name} " +
                     $"vehicle={RouteVehicles[challengeIndex]} ramp={challenge.RecommendedRampDegrees} " +
                     $"passes={passes} score={main.TestRunScore} " +
                     $"broken={string.Join(",", statue.BrokenPartIds)} complete={complete}");
        }
        await RemoveNode(main);
    }

    private async Task<bool> DriveRoutePass(GameRoot main, VehicleController car, float seconds)
    {
        Input.ActionPress("drive_forward");
        float elapsed = 0f;
        for (int frame = 0; frame < 1200; frame++)
        {
            await WaitPhysics(1);
            elapsed += (float)GetPhysicsProcessDeltaTime();
            if (!GodotObject.IsInstanceValid(car))
                break;
            if (main.TestChallengeCompleted)
                break;
            if (elapsed > seconds || car.GlobalPosition.Z < -52f)
                break;
        }
        Input.ActionRelease("drive_forward");
        await WaitPhysics(5);
        return main.TestChallengeCompleted;
    }

    private async Task ReverseRoutePass(VehicleController car, float seconds)
    {
        Input.ActionPress("drive_reverse");
        float elapsed = 0f;
        for (int frame = 0; frame < 700; frame++)
        {
            await WaitPhysics(1);
            elapsed += (float)GetPhysicsProcessDeltaTime();
            if (!GodotObject.IsInstanceValid(car) || elapsed > seconds)
                break;
            if (car.GlobalPosition.Z > -10f)
                break;
        }
        Input.ActionRelease("drive_reverse");
        await WaitPhysics(8);
    }

    private async Task TestCameraAndEffects()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        main.TestSelectChallenge(3);
        await WaitPhysics(15);
        VehicleController? car = main.CurrentVehicle;
        Check(car != null, "camera test has a vehicle");
        if (car == null)
        {
            await RemoveNode(main);
            return;
        }
        Input.ActionPress("drive_forward");
        for (int i = 0; i < 600 && main.TestRunScore == 0; i++)
            await WaitPhysics(1);
        Input.ActionRelease("drive_forward");
        await WaitPhysics(8);
        Check(main.TestEffects.ActivePuffs <= main.TestEffects.MaxActivePuffs &&
              main.TestEffects.ActiveChips <= main.TestEffects.MaxActiveChips &&
              main.TestEffects.ActiveFlashes <= main.TestEffects.MaxActiveFlashes &&
              main.TestEffects.ActiveSparks <= main.TestEffects.MaxActiveSparks,
            "impact effects stay within authored bounds");
        Check(main.TestCamera.GlobalPosition.Y > 2.3f,
            "camera never drops below the arena after an impact");
        if (main.TestCinematicTimer > 0f)
        {
            await TapKey(Key.C);
            Check(main.TestCinematicTimer <= 0f,
                "C skips the impact close-up immediately");
        }
        await WaitPhysics(140);
        float distance = main.TestCamera.GlobalPosition.DistanceTo(car.GlobalPosition);
        Check(distance < 32f && main.TestCamera.GlobalPosition.Y > 2.3f,
            $"camera recovers to a usable chase distance ({distance:0.0} m)");
        Check(main.TestEffects.ActivePuffs == 0 && main.TestEffects.ActiveChips == 0 &&
              main.TestEffects.ActiveFlashes == 0 && main.TestEffects.ActiveSparks == 0,
            "dust, chips, sparks and flashes all clean themselves up");
        await RemoveNode(main);
    }

    private async Task TestRetryBounds()
    {
        GameRoot main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(main);
        await WaitPhysics(4);
        bool stable = true;
        int nodesBefore = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
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
        int nodesAfter = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        Check(stable, "30 retries keep statue/vehicle state bounded and intact");
        Check(nodesAfter - nodesBefore < 60,
            $"30 retries do not accumulate scene nodes ({nodesBefore}->{nodesAfter})");
        Check(Engine.TimeScale == 1.0, "retry restores normal time scale");
        Check(elapsed < 2000, $"30 retries complete quickly ({elapsed} ms total)");
        await RemoveNode(main);
    }


    private async Task RemoveNode(Node node)
    {
        if (GodotObject.IsInstanceValid(node))
            node.QueueFree();
        // Let both the idle tree and the physics server finish disposing before
        // a later test instantiates another Main scene. This keeps the Godot
        // ScriptManagerBridge from racing a freed script handle.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
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
