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
        float angle = EnvFloat("RW_VISUAL_ANGLE", -1f);
        bool freeSmash = EnvInt("RW_VISUAL_FREE", 0) == 1;
        string tag = Env("RW_VISUAL_TAG", $"v{vehicle}_c{challenge}");

        _main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(_main);
        ApplyDiagnostics();
        await WaitPhysics(6);
        _main.Profile.Data.VehiclesUnlocked[2] = true;
        _main.TestSelectVehicle(vehicle);
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
        if (angle >= 0f)
            _main.TestSetRampDegrees(angle);
        if (EnvInt("RW_VISUAL_QUALITY", -1) >= 0)
            _main.TestSetQuality(EnvInt("RW_VISUAL_QUALITY", 0));
        await WaitPhysics(35);
        await Capture($"{tag}_01_start.png");

        if (EnvInt("RW_VISUAL_STATUE", 0) == 1)
        {
            _main.TestShowcaseStatue();
            await WaitPhysics(5);
            await Capture($"{tag}_statue.png");
            await Finish();
            return;
        }

        if (EnvInt("RW_VISUAL_HELP", 0) == 1)
        {
            _main.TestSetHelp(true);
            await WaitPhysics(8);
            await Capture($"{tag}_help.png");
            await Finish();
            return;
        }

        if (EnvInt("RW_DIAG_START", 0) == 1)
        {
            await Finish();
            return;
        }

        if (EnvInt("RW_VISUAL_GARAGE", 0) == 1)
        {
            _main.TestSetGarage(true);
            await WaitPhysics(8);
            await Capture($"{tag}_garage.png");
            await Finish();
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
            await Finish();
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
                // Let the collision response move the car clear of the statue
                // before the evidence frame, so the screenshot shows the hit
                // rather than a model intersection on the same physics tick.
                await WaitPhysics(5);
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
        await Finish();
    }

    private void ApplyDiagnostics()
    {
        DirectionalLight3D sun = _main.GetNode<DirectionalLight3D>("Sun");
        if (EnvInt("RW_DIAG_NOSUN", 0) == 1)
            sun.ShadowEnabled = false;
        if (OS.GetEnvironment("RW_DIAG_SUNBIAS") is { Length: > 0 } bias)
            sun.ShadowBias = float.Parse(bias);
        if (OS.GetEnvironment("RW_DIAG_SUNNORMAL") is { Length: > 0 } normal)
            sun.ShadowNormalBias = float.Parse(normal);
        if (OS.GetEnvironment("RW_DIAG_SUNMAX") is { Length: > 0 } max)
            sun.DirectionalShadowMaxDistance = float.Parse(max);
        if (OS.GetEnvironment("RW_DIAG_SUNMODE") is { Length: > 0 } mode)
            sun.DirectionalShadowMode = (DirectionalLight3D.ShadowMode)int.Parse(mode);
        if (OS.GetEnvironment("RW_DIAG_SUNROT") is { Length: > 0 } rot)
        {
            string[] xyz = rot.Split(',');
            sun.Rotation = new Vector3(float.Parse(xyz[0]), float.Parse(xyz[1]), 0);
        }
        if (EnvInt("RW_DIAG_NOOMNI", 0) == 1)
            _main.GetNode<OmniLight3D>("StatueLight").ShadowEnabled = false;
        if (EnvInt("RW_DIAG_NOSHADOW", 0) == 1)
        {
            _main.GetNode<DirectionalLight3D>("Sun").ShadowEnabled = false;
            _main.GetNode<OmniLight3D>("StatueLight").ShadowEnabled = false;
        }
        if (EnvInt("RW_DIAG_NOMSAA", 0) == 1)
            GetViewport().Msaa3D = Viewport.Msaa.Disabled;
        if (EnvInt("RW_DIAG_NOFOG", 0) == 1)
            _main.GetNode<WorldEnvironment>("WorldEnvironment").Environment.FogEnabled = false;
        WorldEnvironment world = _main.GetNode<WorldEnvironment>("WorldEnvironment");
        if (EnvInt("RW_DIAG_NOAMBIENT", 0) == 1)
            world.Environment.AmbientLightEnergy = 0f;
        if (OS.GetEnvironment("RW_DIAG_AMB") is { Length: > 0 } amb)
            world.Environment.AmbientLightEnergy = float.Parse(amb);
        if (OS.GetEnvironment("RW_DIAG_SUNENERGY") is { Length: > 0 } sunEnergy)
            sun.LightEnergy = float.Parse(sunEnergy);
        if (OS.GetEnvironment("RW_DIAG_EXPOSURE") is { Length: > 0 } exposure)
            world.Environment.TonemapExposure = float.Parse(exposure);
        if (OS.GetEnvironment("RW_DIAG_TONEMAP") is { Length: > 0 } tonemap)
            world.Environment.TonemapMode = (Godot.Environment.ToneMapper)int.Parse(tonemap);
        if (EnvInt("RW_DIAG_ENVNOSHADOW", 0) == 1)
            SetCastShadow(_main.GetNode("EnvironmentVisuals"), false);
        if (EnvInt("RW_DIAG_ACTORSNOSHADOW", 0) == 1)
            SetCastShadow(_main.GetNode("Actors"), false);
        if (EnvInt("RW_DIAG_ATLAS8K", 0) == 1)
            RenderingServer.DirectionalShadowAtlasSetSize(8192, false);
        if (OS.GetEnvironment("RW_DIAG_LIGHTANGLE") is { Length: > 0 } angle)
            sun.LightAngularDistance = float.Parse(angle);
    }

    private static void SetCastShadow(Node root, bool enabled)
    {
        if (root is GeometryInstance3D geometry)
            geometry.CastShadow = enabled
                ? GeometryInstance3D.ShadowCastingSetting.On
                : GeometryInstance3D.ShadowCastingSetting.Off;
        foreach (Node child in root.GetChildren())
            SetCastShadow(child, enabled);
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


    private async Task Finish()
    {
        if (IsInstanceValid(_main))
        {
            _main.QueueFree();
            for (int i = 0; i < 12; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetTree().Quit(0);
    }
}
