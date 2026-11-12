using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace RampWreckers.Tests;

/// <summary>
/// Human-style playtest harness for the v0.1.1 playtest plan. Boots the real
/// Main.tscn against a fresh save (RW_SAVE_PATH chosen by the caller) and plays
/// it using injected keyboard and mouse events only — no Test* gameplay
/// shortcuts, no scripted positioning — capturing screenshots, timings, HUD
/// state and technical telemetry at each phase of the plan.
///
/// This scene is a manual tool, not part of the release game: it is not the
/// main scene, it is never autoloaded, and nothing under src/ references it, so
/// shipping builds never load or execute it. Env-gated diagnostic modes:
///   RW_PT_VERIFY=1  check a save written by the previous run
///   RW_PT_STEER=1   steering authority probe
///   RW_PT_ROBOT=1   robot tier angle sweep
///   RW_PT_CORE=1    Core Shot strike probe
///
/// Run windowed (not --headless):
///   RW_SAVE_PATH=/tmp/fresh.json godot --path . res://tests/PlaytestRun.tscn
/// Verify save persistence with a second short run:
///   RW_PT_VERIFY=1 RW_SAVE_PATH=<same path> godot --path . res://tests/PlaytestRun.tscn
/// </summary>
public partial class PlaytestRun : Node
{
    private GameRoot _main = null!;
    private string _outDir = string.Empty;
    private readonly List<int> _fpsSamples = new();
    private readonly List<string> _findings = new();

    public override void _Ready()
    {
        _outDir = ProjectSettings.GlobalizePath("res://artifacts/playtest");
        Directory.CreateDirectory(_outDir);
        Callable.From(() => { _ = Run(); }).CallDeferred();
    }

    private async Task Run()
    {
        _main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(_main);

        if (EnvInt("RW_PT_VERIFY", 0) == 1)
        {
            await VerifySaveAcrossSessions();
            await Quit();
            return;
        }

        if (EnvInt("RW_PT_STEER", 0) == 1)
        {
            await SteeringProbe();
            await Quit();
            return;
        }

        if (EnvInt("RW_PT_ROBOT", 0) == 1)
        {
            await RobotProbe();
            await Quit();
            return;
        }

        if (EnvInt("RW_PT_CORE", 0) == 1)
        {
            await CoreProbe();
            await Quit();
            return;
        }

        await Phase0_FirstBoot();
        await Phase1_BlindFirstRun();
        await Phase2_DrivingAndRamp();
        await Phase3_DestructionFeel();
        await Phase4_Challenges();
        await Phase5_GarageAndProgression();
        await Phase6_FreeSmash();
        await Phase7_Report();
        await Quit();
    }

    // ------------------------------------------------------------------
    // Phase 0 — fresh boot, first-minute readability
    // ------------------------------------------------------------------
    private async Task Phase0_FirstBoot()
    {
        Pt("phase=p0_boot starting");
        await WaitPhysics(45);
        await Capture("p0_boot.png");

        StatueController statue = Statue()!;
        Pt("phase=p0", Stat("vehicle", Vehicle()!.DisplayName),
            Stat("statue", statue.DisplayName), Stat("statue_parts", statue.PartCount),
            Stat("coins", Profile().Data.Coins),
            Stat("unlocked", string.Join("", Profile().Data.VehiclesUnlocked.Select(b => b ? '1' : '0'))),
            Stat("ramp_deg", _main.TestRampDegrees),
            Stat("challenge", _main.CurrentChallengeIndex + 1),
            "msg", _main.TestStatus);

        KeyPress(Key.H, true);
        await WaitPhysics(3);
        KeyPress(Key.H, false);
        await WaitPhysics(10);
        Pt("phase=p0 help_open", Stat("help_visible", _main.TestHelpOpen));
        await Capture("p0_help.png");
        KeyPress(Key.H, true);
        await WaitPhysics(3);
        KeyPress(Key.H, false);
        await WaitPhysics(6);
        Pt("phase=p0 help_closed", Stat("help_visible", _main.TestHelpOpen));
    }

    // ------------------------------------------------------------------
    // Phase 1 — blind first run: only "hold W" knowledge, like a child mashing
    // ------------------------------------------------------------------
    private async Task Phase1_BlindFirstRun()
    {
        Pt("phase=p1_blind starting");
        await WaitPhysics(20);
        VehicleController car = Vehicle()!;
        Vector3 start = car.GlobalPosition;

        bool launched = false;
        int launchFrame = -1;
        float apex = 0f;
        bool capturedRamp = false, capturedAir = false, capturedImpact = false;

        KeyPress(Key.W, true);
        for (int i = 0; i < 900; i++)
        {
            await WaitPhysics(1);
            SampleFps();
            car = Vehicle()!;
            if (car == null)
                break;
            apex = MathF.Max(apex, car.GlobalPosition.Y);
            if (!launched && car.IsAirborne)
            {
                launched = true;
                launchFrame = i;
            }
            if (!capturedRamp && car.GlobalPosition.Z < 10.5f)
            {
                capturedRamp = true;
                await Capture("p1_ramp.png");
            }
            if (launched && !capturedAir && car.GlobalPosition.Y > 2.0f)
            {
                capturedAir = true;
                await Capture("p1_air.png");
            }
            if (!capturedImpact && _main.TestRunScore > 0)
            {
                capturedImpact = true;
                await WaitPhysics(6);
                await Capture("p1_impact.png");
            }
            if (_main.TestChallengeCompleted)
                break;
        }
        KeyPress(Key.W, false);
        await WaitPhysics(40);

        StatueController statue = Statue()!;
        Pt("phase=p1_result",
            Stat("launched", launched), Stat("launch_frames", launchFrame),
            Stat("apex_m", Round(apex)), Stat("score", _main.TestRunScore),
            Stat("parts_broken", statue.BrokenCount), Stat("challenge1_complete", _main.TestChallengeCompleted),
            "msg", _main.TestStatus);
        await Capture("p1_result.png");

        // First-retry experience: R must reset quickly and completely.
        int brokenBefore = Statue()!.BrokenCount;
        int retryLatency = await MeasureRetryLatency();
        Pt("phase=p1_retry", Stat("latency_frames", retryLatency),
            Stat("statue_restored", Statue()!.BrokenCount == 0), Stat("broken_before", brokenBefore));
    }

    // ------------------------------------------------------------------
    // Phase 2 — driving feel and ramp physics for every vehicle
    // ------------------------------------------------------------------
    private async Task Phase2_DrivingAndRamp()
    {
        Pt("phase=p2 starting");

        // Reverse control.
        VehicleController car = Vehicle()!;
        float zBefore = car.GlobalPosition.Z;
        KeyPress(Key.S, true);
        await WaitPhysics(80);
        KeyPress(Key.S, false);
        await WaitPhysics(10);
        Pt("phase=p2_reverse", Stat("z_moved", Round(car.GlobalPosition.Z - zBefore)),
            Stat("gear_text", GearLabel()));

        // Steering authority at speed.
        await TapKey(Key.R);
        await WaitPhysics(15);
        KeyPress(Key.W, true);
        await WaitPhysics(60);
        float yawBefore = HeadingYaw(car);
        KeyPress(Key.D, true);
        await WaitPhysics(45);
        KeyPress(Key.D, false);
        await WaitPhysics(15);
        KeyPress(Key.W, false);
        float yawAfter = HeadingYaw(car);
        Pt("phase=p2_steer", Stat("yaw_change_deg", Round(Godot.Mathf.RadToDeg(yawAfter - yawBefore))),
            Stat("speed_kph", Round(car.SpeedKph)));

        // Ramp keys Q/E and feedback text.
        await TapKey(Key.Q);
        await WaitPhysics(3);
        await TapKey(Key.Q);
        await WaitPhysics(6);
        string lowMsg = _main.TestStatus;
        for (int i = 0; i < 5; i++)
        {
            await TapKey(Key.E);
            await WaitPhysics(3);
        }
        string highMsg = _main.TestStatus;
        Pt("phase=p2_ramp_keys", "low_msg", lowMsg, "high_msg", highMsg,
            Stat("ramp_deg", _main.TestRampDegrees));
        await Capture("p2_ramp_adjust.png");

        // Each vehicle up the ramp at the default angle.
        int[] vehicles = { 1, 0, 2 };
        foreach (int v in vehicles)
        {
            await SelectVehicleByKeys(v);
            await TapKey(Key.R);
            await WaitPhysics(15);
            car = Vehicle()!;

            var run = await DriveFullRun("p2_v" + v, maxFrames: 600, steerFrames: 0, steerDir: 0);
            Pt("phase=p2_vehicle", Stat("vehicle_index", v),
                Stat("airborne", run.Launched), Stat("apex_m", Round(run.Apex)), Stat("speed_at_ramp_kph", Round(run.SpeedAtRamp)),
                Stat("score", _main.TestRunScore),
                Stat("parts_broken", Statue()!.BrokenCount),
                Stat("flipped_or_stuck", run.StuckFrames > 90),
                Stat("stuck_frames", run.StuckFrames),
                "final_msg", _main.TestStatus);
            await Capture($"p2_v{v}_after.png");

            // Locked-vehicle feedback check for the monster truck.
            if (v == 2 && !Profile().Data.VehiclesUnlocked[2])
            {
                await SelectVehicleByKeys(2);
                Pt("phase=p2_locked", "msg", _main.TestStatus,
                    Stat("still_vehicle", Vehicle()!.VehicleIndex));
            }
        }
    }

    // ------------------------------------------------------------------
    // Phase 3 — destruction feel per vehicle (score, debris, cinematic)
    // ------------------------------------------------------------------
    private async Task Phase3_DestructionFeel()
    {
        Pt("phase=p3 starting");
        foreach (int v in new[] { 1, 0 })
        {
            await SelectVehicleByKeys(v);
            await TapKey(Key.R);
            await WaitPhysics(15);
            var run = await DriveFullRun($"p3_v{v}", maxFrames: 600, steerFrames: 0, steerDir: 0,
                captureImpact: true);
            StatueController statue = Statue()!;
            Pt("phase=p3_destruction", Stat("vehicle_index", v),
                Stat("score", _main.TestRunScore),
                Stat("parts_broken", statue.BrokenCount),
                Stat("broken_ids", string.Join(",", statue.BrokenPartIds.OrderBy(s => s))),
                Stat("cinematic_started", _main.TestCinematicTimer > 0f),
                Stat("collapsed", statue.IsCollapsed),
                "msg", _main.TestStatus);
            await WaitPhysics(30);
            await Capture($"p3_v{v}_debris.png");

            // C must skip the impact close-up and restore responsive play.
            await TapKey(Key.C);
            await WaitPhysics(4);
            Pt("phase=p3_cinematic_skip", Stat("cinematic_after_c", _main.TestCinematicTimer > 0f));
        }
    }

    // ------------------------------------------------------------------
    // Phase 4 — all nine campaign challenges via ordinary controls
    // ------------------------------------------------------------------
    private async Task Phase4_Challenges()
    {
        Pt("phase=p4 starting");
        // The harness follows the catalog's own HUD recommendations: the
        // recommended vehicle and the recommended ramp angle (auto-set on
        // challenge select). Only the Core Shot double hit is scripted, and
        // everything is driven with plain key events.
        var plan = new (int Vehicle, bool MultiHit)[]
        {
            (ChallengeCatalog.All[0].RecommendedVehicle, false), // First Crash
            (ChallengeCatalog.All[1].RecommendedVehicle, false), // Hat Trick
            (ChallengeCatalog.All[2].RecommendedVehicle, false), // Gnome Down
            (ChallengeCatalog.All[3].RecommendedVehicle, false), // Shield Breaker — straight, no steering
            (ChallengeCatalog.All[4].RecommendedVehicle, false), // Knee Trouble
            (ChallengeCatalog.All[5].RecommendedVehicle, false), // Knightfall
            (ChallengeCatalog.All[6].RecommendedVehicle, false), // Peel the Armour
            (ChallengeCatalog.All[7].RecommendedVehicle, true),  // Core Shot — armour, then the core
            (ChallengeCatalog.All[8].RecommendedVehicle, false), // Robot Wreck
        };

        for (int index = 0; index < plan.Length; index++)
        {
            // If the catalog's recommended vehicle is locked and affordable,
            // buy it first — that is what the HUD is telling the player to do.
            WreckChallenge challenge = ChallengeCatalog.All[index];
            if (index >= 6 && !Profile().Data.VehiclesUnlocked[challenge.RecommendedVehicle] &&
                Profile().Data.Coins >= 600)
            {
                Pt("phase=p4", "note", $"buying recommended {challenge.RecommendedVehicleName} for {challenge.Name}");
                await OpenGarageAndBuy();
            }
            if (!Profile().Data.VehiclesUnlocked[challenge.RecommendedVehicle])
                Pt("phase=p4", "note", $"{challenge.RecommendedVehicleName} locked; attempting {challenge.Name} with what we have.");

            while (_main.CurrentChallengeIndex != index)
            {
                await TapKey(Key.K);
                await WaitPhysics(4);
            }
            string hudChallenge = ChallengeLabel();
            await SelectVehicleByKeys(plan[index].Vehicle);
            await TapKey(Key.R);
            await WaitPhysics(15);

            int attempts = 0;
            bool completed = false;
            string lastMsg = string.Empty;
            for (int attempt = 0; attempt < 4 && !completed; attempt++)
            {
                attempts++;
                var run = await DriveFullRun($"p4_c{index + 1}_a{attempt}", maxFrames: 520,
                    steerFrames: 0, steerDir: 0,
                    captureImpact: attempt == 0, multiHit: plan[index].MultiHit);
                completed = _main.TestChallengeCompleted;
                lastMsg = _main.TestStatus;
                if (!completed && Statue()!.BrokenCount > 0)
                    await Capture($"p4_c{index + 1}_a{attempt}_partial.png");
                if (!completed && attempt < 3)
                {
                    await TapKey(Key.R);
                    await WaitPhysics(12);
                }
            }

            int medal = Profile().Data.BestMedals[index];
            Pt("phase=p4_challenge", Stat("index", index + 1), "hud", hudChallenge,
                "vehicle", challenge.RecommendedVehicleName,
                Stat("ramp_deg", _main.TestRampDegrees),
                Stat("attempts", attempts), Stat("completed", completed),
                Stat("medal", medal), Stat("coins", Profile().Data.Coins),
                Stat("score", _main.TestRunScore), "msg", lastMsg);
        }
        await Capture("p4_final.png");
    }

    // ------------------------------------------------------------------
    // Phase 5 — garage, upgrades, paint
    // ------------------------------------------------------------------
    private async Task Phase5_GarageAndProgression()
    {
        Pt("phase=p5 starting");
        await TapKey(Key.G);
        await WaitPhysics(12);
        Pt("phase=p5_open", Stat("garage_visible", _main.TestGarageOpen),
            "coins_label", GarageCoinsText());
        await Capture("p5_garage.png");

        await TryBuyButton("Hud/Garage/GarageVBox/UpgradeRow/EngineButton", "engine");
        await TryBuyButton("Hud/Garage/GarageVBox/UpgradeRow/GripButton", "tyres");
        await TryBuyButton("Hud/Garage/GarageVBox/UpgradeRow/PaintButton", "paint");
        if (!Profile().Data.VehiclesUnlocked[2])
            await TryBuyButton("Hud/Garage/GarageVBox/VehicleRow/MonsterButton", "monster");

        Pt("phase=p5_after_buy",
            Stat("coins", Profile().Data.Coins),
            Stat("engine", Profile().Data.EngineLevel),
            Stat("grip", Profile().Data.GripLevel),
            Stat("paint_unlocked", Profile().Data.PaintUnlocked),
            Stat("monster_unlocked", Profile().Data.VehiclesUnlocked[2]));
        await Capture("p5_garage_after.png");

        KeyPress(Key.Escape, true);
        await WaitPhysics(3);
        KeyPress(Key.Escape, false);
        await WaitPhysics(6);
        Pt("phase=p5_close", Stat("garage_visible", _main.TestGarageOpen));

        // Paint cycling with P.
        await SelectVehicleByKeys(0);
        for (int i = 0; i < 3; i++)
        {
            await TapKey(Key.P);
            await WaitPhysics(4);
            Pt("phase=p5_paint", "msg", _main.TestStatus);
        }
        await Capture("p5_paint.png");

        // Upgraded vehicle should measure faster: bench run on the flat.
        await TapKey(Key.R);
        await WaitPhysics(15);
        VehicleController car = Vehicle()!;
        float z0 = car.GlobalPosition.Z;
        float t0 = Engine.GetPhysicsFrames();
        KeyPress(Key.W, true);
        await WaitPhysics(120);
        KeyPress(Key.W, false);
        float distance = z0 - car.GlobalPosition.Z;
        float seconds = (Engine.GetPhysicsFrames() - t0) / 60f;
        Pt("phase=p5_upgrade_bench", Stat("distance_m", Round(distance)),
            Stat("seconds", Round(seconds)), Stat("avg_kph", Round(distance / seconds * 3.6f)));
    }

    // ------------------------------------------------------------------
    // Phase 6 — Free Smash sandbox
    // ------------------------------------------------------------------
    private async Task Phase6_FreeSmash()
    {
        Pt("phase=p6 starting");
        int coinsBefore = Profile().Data.Coins;
        await TapKey(Key.F);
        await WaitPhysics(8);
        Pt("phase=p6_enter", Stat("free_smash", _main.IsFreeSmash),
            "mode_label", ModeLabel(), "msg", _main.TestStatus);
        await Capture("p6_free_smash.png");

        // Statue switching with J/K.
        await TapKey(Key.J);
        await WaitPhysics(6);
        string statueMsg = _main.TestStatus;
        await TapKey(Key.J);
        await WaitPhysics(6);
        Pt("phase=p6_switch", "first_msg", statueMsg, "second_msg", _main.TestStatus,
            Stat("statue", Statue()!.DisplayName));

        // Smash and confirm no coins are minted.
        var run = await DriveFullRun("p6_smash", maxFrames: 500, steerFrames: 0, steerDir: 0,
            captureImpact: true);
        Pt("phase=p6_smash", Stat("score", _main.TestRunScore),
            Stat("parts_broken", Statue()!.BrokenCount),
            Stat("coins_after", Profile().Data.Coins), Stat("coins_before", coinsBefore),
            Stat("no_coins_minted", Profile().Data.Coins == coinsBefore));
        await WaitPhysics(30);
        await Capture("p6_debris.png");

        // Rapid repeated retries.
        for (int i = 0; i < 3; i++)
        {
            int latency = await MeasureRetryLatency();
            Pt("phase=p6_retry", Stat("attempt", i + 1), Stat("latency_frames", latency),
                Stat("statue_restored", Statue()!.BrokenCount == 0));
        }

        await TapKey(Key.F);
        await WaitPhysics(8);
        Pt("phase=p6_exit", Stat("free_smash", _main.IsFreeSmash), "msg", _main.TestStatus);
    }

    // ------------------------------------------------------------------
    // Phase 7 — summary and telemetry
    // ------------------------------------------------------------------
    private async Task Phase7_Report()
    {
        await WaitPhysics(20);
        await Capture("p7_final.png");
        int min = _fpsSamples.Count > 0 ? _fpsSamples.Min() : 0;
        int max = _fpsSamples.Count > 0 ? _fpsSamples.Max() : 0;
        double avg = _fpsSamples.Count > 0 ? _fpsSamples.Average() : 0;
        Pt("phase=p7_fps", Stat("samples", _fpsSamples.Count),
            Stat("min", min), Stat("max", max), Stat("avg", System.Math.Round(avg, 2)));
        Pt("phase=p7_save", Stat("coins", Profile().Data.Coins),
            Stat("medals", string.Join(",", Profile().Data.BestMedals)),
            Stat("claimed", string.Join("", Profile().Data.RewardClaimed.Select(b => b ? '1' : '0'))),
            Stat("monster", Profile().Data.VehiclesUnlocked[2]),
            Stat("engine", Profile().Data.EngineLevel), Stat("grip", Profile().Data.GripLevel),
            Stat("paint", Profile().Data.PaintUnlocked));
        foreach (string finding in _findings)
            Pt("phase=p7_finding", "finding", finding);
        GD.Print("[PT] SUMMARY_END");
    }

    /// <summary>
    /// Compares rally vs monster on the robot at 10°: where the car is, how
    /// fast it is going and which parts give way when contact happens.
    /// </summary>
    /// <summary>
    /// Core Shot sweep: rally flights peel the armour (core then drops to the
    /// ground); monster collapses the robot (armour and core detach intact).
    /// Either way a later ground ram should smash the fallen core.
    /// </summary>
    private async Task CoreProbe()
    {
        await WaitPhysics(20);
        while (_main.CurrentChallengeIndex != 7)
        {
            await TapKey(Key.K);
            await WaitPhysics(3);
        }
        await WaitPhysics(8);

        foreach ((int v, float angle) in new[] { (0, 20f) })
        {
            for (int t = 0; t < 8 && _main.TestRampDegrees != angle; t++)
            {
                await TapKey(_main.TestRampDegrees < angle ? Key.E : Key.Q);
                await WaitPhysics(2);
            }
            await SelectVehicleByKeys(v);
            await TapKey(Key.R);
            await WaitPhysics(15);

            // Phase 1: flight strike peels the chest armour.
            StatueController watched = Statue()!;
            watched.PartBlocked += (part, protector) => Pt("phase=core_blocked", part, protector);
            watched.PartResisted += (part, energy) => Pt("phase=core_resisted", part, Stat("energy", Round(energy)));
            watched.PartBroken += (part, score, weak) => Pt("phase=core_broken", part, Stat("score", score));

            for (int pass = 0; pass < 4; pass++)
            {
                KeyPress(Key.W, true);
                for (int i = 0; i < 340; i++)
                {
                    await WaitPhysics(1);
                    if (_main.TestChallengeCompleted)
                        break;
                }
                KeyPress(Key.W, false);
                Pt("phase=core_ram", Stat("pass", pass),
                    Stat("score", _main.TestRunScore),
                    Stat("complete", _main.TestChallengeCompleted),
                    "broken", string.Join(",", Statue()!.BrokenPartIds.OrderBy(s => s)));
                if (_main.TestChallengeCompleted)
                    break;
                // Route-style reverse (keeps the car facing forward, in bounds).
                KeyPress(Key.S, true);
                await WaitPhysics(190);
                KeyPress(Key.S, false);
                await WaitPhysics(12);
            }
            Pt("phase=core_result", Stat("vehicle", v), Stat("angle", angle),
                Stat("score", _main.TestRunScore),
                Stat("complete", _main.TestChallengeCompleted),
                "broken", string.Join(",", Statue()!.BrokenPartIds.OrderBy(s => s)));
            await Capture($"core_probe_v{v}_a{angle:0}.png");
            await TapKey(Key.R);
            await WaitPhysics(15);
        }
    }

    private async Task RobotProbe()
    {
        Profile().Data.VehiclesUnlocked[2] = true;
        await WaitPhysics(20);
        while (_main.CurrentChallengeIndex != 8)
        {
            await TapKey(Key.K);
            await WaitPhysics(3);
        }
        await WaitPhysics(8);

        foreach (int v in new[] { 0, 2 })
        {
            foreach (float angle in new[] { 10f, 16f, 20f })
            {
                for (int t = 0; t < 8 && _main.TestRampDegrees != angle; t++)
                {
                    await TapKey(_main.TestRampDegrees < angle ? Key.E : Key.Q);
                    await WaitPhysics(2);
                }
                await SelectVehicleByKeys(v);
                await TapKey(Key.R);
                await WaitPhysics(15);
                VehicleController car = Vehicle()!;
                StatueController statue = Statue()!;
                int brokeEvents = 0;
                string firstBreakAt = "";
                int partsAtEnd = 0;

                car.BodyEntered += body =>
                    Pt("phase=robot_contact", Stat("vehicle", v), Stat("angle", angle),
                        "body", body.Name, "body_class", body.GetType().Name,
                        Stat("car_z", Round(car.GlobalPosition.Z)),
                        Stat("car_y", Round(car.GlobalPosition.Y)),
                        Stat("kph", Round(car.SpeedKph)));

                KeyPress(Key.W, true);
                for (int i = 0; i < 700; i++)
                {
                    await WaitPhysics(1);
                    car = Vehicle()!;
                    if (car == null)
                        break;
                    if (i % 30 == 0)
                        Pt("phase=robot_track", Stat("vehicle", v), Stat("angle", angle),
                            Stat("frame", i), Stat("z", Round(car.GlobalPosition.Z)),
                            Stat("y", Round(car.GlobalPosition.Y)),
                            Stat("x", Round(car.GlobalPosition.X)),
                            Stat("kph", Round(car.SpeedKph)),
                            Stat("air", car.IsAirborne), Stat("sleep", car.Sleeping),
                            Stat("parts", statue.BrokenCount));
                    int broken = statue.BrokenCount;
                    if (broken > partsAtEnd)
                    {
                        brokeEvents++;
                        Vector3 p = car.GlobalPosition;
                        string detail = $"break#{brokeEvents} frame={i} car_y={Round(p.Y)} " +
                                        $"car_z={Round(p.Z)} car_x={Round(p.X)} " +
                                        $"kph={Round(car.SpeedKph)} grounded={car.IsGrounded}";
                        if (brokeEvents == 1)
                            firstBreakAt = detail;
                        Pt("phase=robot_break", Stat("vehicle", v), Stat("angle", angle),
                            "detail", detail,
                            "broken_now", string.Join(",", statue.BrokenPartIds));
                        partsAtEnd = broken;
                    }
                    if (car.GlobalPosition.Z < -30f || i > 660)
                        break;
                }
                KeyPress(Key.W, false);
                await WaitPhysics(120);
                Pt("phase=robot_result", Stat("vehicle", v), Stat("angle", angle),
                    Stat("score", _main.TestRunScore), Stat("parts", Statue()!.BrokenCount),
                    Stat("collapsed", Statue()!.IsCollapsed), "first", firstBreakAt,
                    "broken", string.Join(",", Statue()!.BrokenPartIds.OrderBy(s => s)));
                await Capture($"robot_probe_v{v}_a{angle:0}.png");
            }
        }
    }

    private async Task SteeringProbe()
    {
        // Measures how fast heading actually changes with steer input held,
        // at three speeds, using injected keys only.
        await WaitPhysics(40);
        await SelectVehicleByKeys(1); // pickup
        StatueController statue = Statue()!;
        Pt("phase=steer_probe", "statue_side_note",
            "shield screen side check below");
        // Where does the knight's shield sit on screen? (right = +x)
        statue = Statue()!;
        if (_main.CurrentChallengeIndex != 3)
        {
            while (_main.CurrentChallengeIndex != 3)
            {
                await TapKey(Key.K);
                await WaitPhysics(3);
            }
        }
        await WaitPhysics(10);
        var camera = _main.TestCamera;
        StatuePart? shield = Statue()!.Parts.TryGetValue("Shield", out StatuePart? s) ? s : null;
        if (shield != null)
        {
            Vector2 screen = camera.UnprojectPosition(shield.GlobalPosition);
            Pt("phase=shield_screen", Stat("x", Round(screen.X)),
                Stat("y", Round(screen.Y)),
                Stat("viewport_w", GetViewport().GetVisibleRect().Size.X),
                Stat("world", shield.GlobalPosition.ToString()));
        }
        await Capture("steer_shield_view.png");

        // Now the actual steering authority sweep.
        while (_main.CurrentChallengeIndex != 0)
        {
            await TapKey(Key.J);
            await WaitPhysics(3);
        }
        await TapKey(Key.R);
        await WaitPhysics(20);
        VehicleController car = Vehicle()!;
        foreach (int hold in new[] { 0, 30, 60, 120 })
        {
            if (hold > 0)
                KeyPress(Key.W, true);
            await WaitPhysics(hold); // build speed first
            float yaw0 = HeadingYaw(car);
            float angVel0 = car.AngularVelocity.Y;
            float strength0 = Input.GetActionStrength("steer_right");
            float throttle0 = Input.GetActionStrength("drive_forward");
            KeyPress(Key.D, true);
            for (int i = 0; i < 45; i++)
            {
                await WaitPhysics(1);
                if (i % 15 == 14)
                    Pt("phase=steer_probe_frame", Stat("hold_before", hold),
                        Stat("frame", i + 1), Stat("kph", Round(car.SpeedKph)),
                        Stat("yaw_deg", Round(Godot.Mathf.RadToDeg(
                            (HeadingYaw(car) - yaw0 + MathF.PI) % (2 * MathF.PI) - MathF.PI))),
                        Stat("ang_vel_y", Round(car.AngularVelocity.Y)),
                        Stat("steer_strength", Input.GetActionStrength("steer_right")),
                        Stat("throttle_strength", Input.GetActionStrength("drive_forward")),
                        Stat("sleeping", car.Sleeping), Stat("grounded", car.IsGrounded));
            }
            KeyPress(Key.D, false);
            Pt("phase=steer_probe_end", Stat("hold_before", hold),
                Stat("total_yaw_deg", Round(Godot.Mathf.RadToDeg(
                    (HeadingYaw(car) - yaw0 + MathF.PI) % (2 * MathF.PI) - MathF.PI))),
                Stat("start_strength", strength0), Stat("start_ang_vel", Round(angVel0)),
                Stat("start_throttle", throttle0));
            await TapKey(Key.R);
            await WaitPhysics(20);
            car = Vehicle()!;
        }

        // Child scenario: sit at the start line a long time, then press W.
        await WaitPhysics(200);
        float yawIdle0 = HeadingYaw(Vehicle()!);
        KeyPress(Key.W, true);
        for (int i = 0; i < 60; i++)
        {
            await WaitPhysics(1);
            if (i % 20 == 19)
                Pt("phase=idle_then_w", Stat("frame", i + 1),
                    Stat("kph", Round(Vehicle()!.SpeedKph)),
                    Stat("sleeping", Vehicle()!.Sleeping),
                    Stat("throttle_strength", Input.GetActionStrength("drive_forward")));
        }
        KeyPress(Key.W, false);
        await TapKey(Key.R);
        await WaitPhysics(10);
        // And the pure standstill case that froze: W and D together, fresh reset.
        VehicleController fresh = Vehicle()!;
        KeyPress(Key.W, true);
        KeyPress(Key.D, true);
        for (int i = 0; i < 60; i++)
        {
            await WaitPhysics(1);
            if (i % 20 == 19)
                Pt("phase=w_and_d_standstill", Stat("frame", i + 1),
                    Stat("kph", Round(fresh.SpeedKph)), Stat("sleeping", fresh.Sleeping),
                    Stat("grounded", fresh.IsGrounded), Stat("yaw_deg", Round(Godot.Mathf.RadToDeg(
                        (HeadingYaw(fresh) + MathF.PI) % (2 * MathF.PI) - MathF.PI))),
                    Stat("throttle_strength", Input.GetActionStrength("drive_forward")));
        }
        KeyPress(Key.W, false);
        KeyPress(Key.D, false);
    }

    private async Task VerifySaveAcrossSessions()
    {
        await WaitPhysics(60);
        GameProfile profile = Profile();
        GD.Print("[PT] VERIFY coins=" + profile.Data.Coins +
                 " medals=" + string.Join(",", profile.Data.BestMedals) +
                 " claimed=" + string.Join("", profile.Data.RewardClaimed.Select(b => b ? '1' : '0')) +
                 " monster=" + profile.Data.VehiclesUnlocked[2] +
                 " engine=" + profile.Data.EngineLevel +
                 " grip=" + profile.Data.GripLevel +
                 " paint=" + profile.Data.PaintUnlocked);
        await Capture("verify_save.png");
    }

    // ------------------------------------------------------------------
    // Shared helpers — real input only
    // ------------------------------------------------------------------
    private async Task<RunResult> DriveFullRun(string tag, int maxFrames, int steerFrames,
        int steerDir, bool captureImpact = false, bool multiHit = false)
    {
        var result = new RunResult();
        await TapKey(Key.R);
        await WaitPhysics(12);
        VehicleController car = Vehicle()!;
        float apex = 0f;
        bool launched = false, capturedAir = false, capturedImpact = false;
        float speedAtRamp = 0f;
        int stuck = 0;
        bool steerPressed = false;

        KeyPress(Key.W, true);
        for (int i = 0; i < maxFrames; i++)
        {
            await WaitPhysics(1);
            SampleFps();
            car = Vehicle()!;
            if (car == null)
                break;
            apex = MathF.Max(apex, car.GlobalPosition.Y);
            if (!launched && car.IsAirborne)
                launched = true;
            if (car.GlobalPosition.Z < 9.5f && speedAtRamp <= 0f)
                speedAtRamp = car.SpeedKph;
            if (car.IsGrounded && car.SpeedKph < 1.2f)
                stuck++;
            else
                stuck = 0;
            if (steerFrames > 0 && steerDir != 0 && i < steerFrames && !steerPressed)
            {
                steerPressed = true;
                KeyPress(steerDir > 0 ? Key.D : Key.A, true);
            }
            if (steerPressed && i >= steerFrames)
            {
                steerPressed = false;
                KeyPress(steerDir > 0 ? Key.D : Key.A, false);
            }
            if (!capturedAir && car.IsAirborne && car.GlobalPosition.Y > 2.0f)
            {
                capturedAir = true;
                if (captureImpact)
                    await Capture(tag + "_air.png");
            }
            bool impacted = _main.TestRunScore > 0 || Statue()!.BrokenCount > 0 ||
                            (car.IsGrounded && car.SpeedKph < 4f && i > 40);
            if (!capturedImpact && impacted)
            {
                capturedImpact = true;
                await WaitPhysics(6);
                if (captureImpact)
                    await Capture(tag + "_impact.png");
            }
            if (_main.TestChallengeCompleted)
                break;
        }
        KeyPress(Key.W, false);
        KeyPress(Key.D, false);
        KeyPress(Key.A, false);
        await WaitPhysics(25);

        result.Launched = launched;
        result.Apex = apex;
        result.SpeedAtRamp = speedAtRamp;
        result.StuckFrames = stuck;

        if (multiHit && !_main.TestChallengeCompleted)
        {
            float apexAfter = await MultiHitRecovery(tag, captureImpact, apex);
            result.Apex = MathF.Max(result.Apex, apexAfter);
        }

        return result;
    }

    /// <summary>Reverse away, drive back in — how a player re-hits without R.</summary>
    private async Task<float> MultiHitRecovery(string tag, bool capture, float apex)
    {
        for (int cycle = 0; cycle < 6 && !_main.TestChallengeCompleted; cycle++)
        {
            VehicleController car = Vehicle()!;
            if (car == null)
                return apex;
            if (car.GlobalPosition.Z > 4f)
                break; // too far back; retry via R would be next attempt
            KeyPress(Key.S, true);
            await WaitPhysics(85);
            KeyPress(Key.S, false);
            await WaitPhysics(8);
            float apexBefore = apex;
            KeyPress(Key.W, true);
            for (int i = 0; i < 150; i++)
            {
                await WaitPhysics(1);
                car = Vehicle()!;
                if (car == null)
                    break;
                apex = MathF.Max(apex, car.GlobalPosition.Y);
                if (_main.TestChallengeCompleted || (_main.TestRunScore > 0 && i > 60))
                    break;
            }
            KeyPress(Key.W, false);
            await WaitPhysics(20);
            if (capture && _main.TestRunScore > 0)
                await Capture(tag + "_rehit.png");
            if (apex <= apexBefore + 0.01f && cycle > 1)
                Pt("phase=recovery", "note", "no further launch height gained on cycle " + cycle);
        }
        return apex;
    }

    private async Task<int> MeasureRetryLatency()
    {
        await TapKey(Key.R);
        VehicleController car = Vehicle()!;
        Vector3 spawn = _main.GetNode<Marker3D>("VehicleSpawn").GlobalPosition;
        for (int i = 0; i < 240; i++)
        {
            await WaitPhysics(1);
            car = Vehicle()!;
            if (car != null && car.GlobalPosition.DistanceTo(spawn) < 1.6f)
                return i;
        }
        return -1;
    }

    private async Task SelectVehicleByKeys(int index)
    {
        Key key = index switch { 0 => Key.Key1, 1 => Key.Key2, _ => Key.Key3 };
        await TapKey(key);
        await WaitPhysics(10);
    }

    private async Task OpenGarageAndBuy()
    {
        await TapKey(Key.G);
        await WaitPhysics(10);
        await TryBuyButton("Hud/Garage/GarageVBox/VehicleRow/MonsterButton", "monster_mid_campaign");
        KeyPress(Key.Escape, true);
        await WaitPhysics(3);
        KeyPress(Key.Escape, false);
        await WaitPhysics(6);
    }

    private async Task TryBuyButton(string path, string tag)
    {
        Button? button = _main.GetNodeOrNull<Button>(path);
        if (button == null)
        {
            Find("garage button missing: " + path);
            return;
        }
        string before = button.Text;
        await Click(button);
        await WaitPhysics(8);
        Pt("phase=buy", "tag", tag, "before", before, "after", button.Text,
            Stat("coins", Profile().Data.Coins), "hint", GarageHintText());
    }

    private async Task Click(Button button)
    {
        Vector2 center = button.GetGlobalRect().GetCenter();
        Vector2 canvasSize = GetViewport().GetVisibleRect().Size;
        Vector2 windowSize = DisplayServer.WindowGetSize();
        Vector2 position = center * (windowSize / canvasSize);

        Input.ParseInputEvent(new InputEventMouseMotion { Position = position, GlobalPosition = position });
        await WaitPhysics(2);
        GetTree().Root.PushInput(new InputEventMouseButton
        {
            Position = position,
            GlobalPosition = position,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        });
        GetTree().Root.PushInput(new InputEventMouseButton
        {
            Position = position,
            GlobalPosition = position,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        });
        await WaitPhysics(4);
    }

    private static void KeyPress(Key key, bool pressed) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Pressed = pressed });

    private async Task TapKey(Key key)
    {
        KeyPress(key, true);
        await WaitPhysics(2);
        KeyPress(key, false);
        await WaitPhysics(2);
    }

    private void SampleFps()
    {
        if (Engine.GetPhysicsFrames() % 30 < 1)
            _fpsSamples.Add((int)Engine.GetFramesPerSecond());
    }

    private static float HeadingYaw(VehicleController car)
    {
        Vector3 forward = -car.GlobalTransform.Basis.Z;
        return Godot.Mathf.Atan2(-forward.X, -forward.Z);
    }

    private VehicleController? Vehicle() => _main.CurrentVehicle;
    private StatueController? Statue() => _main.CurrentStatue;
    private GameProfile Profile() => _main.Profile;

    private string ModeLabel() =>
        _main.GetNode<Label>("Hud/ObjectivePanel/ObjectiveVBox/ModeRow/Mode").Text;

    private string ChallengeLabel() =>
        _main.GetNode<Label>("Hud/ObjectivePanel/ObjectiveVBox/Challenge").Text;

    private string GearLabel() =>
        _main.GetNode<Label>("Hud/SpeedPanel/SpeedVBox/Gear").Text;

    private string GarageCoinsText() =>
        _main.GetNode<Label>("Hud/Garage/GarageVBox/GarageCoins").Text;

    private string GarageHintText() =>
        _main.GetNode<Label>("Hud/Garage/GarageVBox/GarageHint").Text;

    private void Find(string finding)
    {
        _findings.Add(finding);
        GD.Print("[PT-FINDING] " + finding);
    }

    private static string Stat(string key, object value) => $"{key}={value}";
    private static float Round(float value) => Godot.Mathf.Round(value * 100f) / 100f;

    private void Pt(string phase, params object[] rest)
    {
        var parts = new List<string> { phase };
        parts.AddRange(rest.Select(r => r?.ToString() ?? ""));
        GD.Print("[PT] " + string.Join(" ", parts));
    }

    private async Task WaitPhysics(int frames)
    {
        for (int i = 0; i < frames; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task Capture(string fileName)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Image image = GetViewport().GetTexture().GetImage();
        string path = System.IO.Path.Combine(_outDir, fileName);
        image.SavePng(path);
        GD.Print("[PT] saved " + path);
    }

    private async Task Quit()
    {
        if (IsInstanceValid(_main))
        {
            _main.QueueFree();
            for (int i = 0; i < 12; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetTree().Quit(0);
    }

    private sealed class RunResult
    {
        public bool Launched;
        public float Apex;
        public float SpeedAtRamp;
        public int StuckFrames;
    }

    private static int EnvInt(string key, int fallback) =>
        int.TryParse(OS.GetEnvironment(key), out int value) ? value : fallback;
}
