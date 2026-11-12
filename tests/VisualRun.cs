using System;
using System.IO;
using System.Threading.Tasks;
using Godot;

namespace RampWreckers.Tests;

/// <summary>
/// Captures human-readable evidence screenshots with a real rendered viewport.
/// Run windowed (not --headless):
///   RW_VISUAL_VEHICLE=1 RW_VISUAL_CHALLENGE=0 godot --path . res://tests/VisualRun.tscn
/// </summary>
public partial class VisualRun : Node
{
    private GameRoot _main = null!;
    private string _outDir = string.Empty;

    public override void _Ready()
    {
        _outDir = ProjectSettings.GlobalizePath("res://artifacts/screenshots");
        Directory.CreateDirectory(_outDir);
        Callable.From(() => { _ = Run(); }).CallDeferred();
    }

    private async Task Run()
    {
        int vehicle = EnvInt("RW_VISUAL_VEHICLE", 1);
        int challenge = EnvInt("RW_VISUAL_CHALLENGE", 0);
        float angle = EnvFloat("RW_VISUAL_ANGLE", 16f);
        bool freeSmash = EnvInt("RW_VISUAL_FREE", 0) == 1;
        string tag = Env("RW_VISUAL_TAG", $"v{vehicle}_c{challenge}");

        _main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(_main);
        await WaitPhysics(6);
        _main.Profile.Data.VehiclesUnlocked[2] = true;
        _main.TestSelectVehicle(vehicle);
        _main.TestSetRampDegrees(angle);
        if (freeSmash)
        {
            if (_main.CurrentChallengeIndex != challenge)
                _main.TestSelectChallenge(challenge);
            _main.TestSpawnStatueIndex(challenge % 3);
        }
        else
        {
            _main.TestSelectChallenge(challenge);
        }
        await WaitPhysics(35);
        await Capture($"{tag}_01_start.png");

        if (EnvInt("RW_VISUAL_GARAGE", 0) == 1)
        {
            _main.TestSetGarage(true);
            await WaitPhysics(8);
            await Capture($"{tag}_garage.png");
            GetTree().Quit(0);
            return;
        }

        if (EnvInt("RW_VISUAL_PERF", 0) == 1)
        {
            Input.ActionPress("drive_forward");
            for (int i = 0; i < 300; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ActionRelease("drive_forward");
            GD.Print($"[PERF] fps={Engine.GetFramesPerSecond():0} " +
                     $"objects={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} " +
                     $"draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}");
            GetTree().Quit(0);
            return;
        }

        Input.ActionPress("drive_forward");
        bool capturedRamp = false;
        bool capturedAir = false;
        bool capturedImpact = false;
        int frames = 0;
        while (frames < 900 && !_main.TestChallengeCompleted)
        {
            await WaitPhysics(1);
            frames++;
            VehicleController? car = _main.CurrentVehicle;
            if (car == null)
                break;
            if (!capturedRamp && car.GlobalPosition.Z < 12f)
            {
                capturedRamp = true;
                await Capture($"{tag}_02_ramp.png");
            }
            if (!capturedAir && car.IsAirborne && car.GlobalPosition.Y > 2.2f)
            {
                capturedAir = true;
                await Capture($"{tag}_03_airborne.png");
            }
            if (!capturedImpact && _main.TestRunScore > 0)
            {
                capturedImpact = true;
                await Capture($"{tag}_04_impact.png");
                await WaitPhysics(20);
                await Capture($"{tag}_05_debris.png");
            }
            if (capturedImpact && frames > 260)
                break;
        }
        Input.ActionRelease("drive_forward");
        await WaitPhysics(30);
        await Capture($"{tag}_06_result.png");
        GD.Print($"[VISUAL] {tag}: score={_main.TestRunScore} completed={_main.TestChallengeCompleted} " +
                 $"frames={frames} frames, fps={Engine.GetFramesPerSecond():0}");
        GetTree().Quit(0);
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
        string path = Path.Combine(_outDir, fileName);
        image.SavePng(path);
        GD.Print("[VISUAL] saved " + path);
    }

    private static string Env(string key, string fallback) =>
        string.IsNullOrEmpty(OS.GetEnvironment(key)) ? fallback : OS.GetEnvironment(key);

    private static int EnvInt(string key, int fallback) =>
        int.TryParse(OS.GetEnvironment(key), out int value) ? value : fallback;

    private static float EnvFloat(string key, float fallback) =>
        float.TryParse(OS.GetEnvironment(key), out float value) ? value : fallback;
}
