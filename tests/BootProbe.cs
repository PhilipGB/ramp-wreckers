using System;
using System.Threading.Tasks;
using Godot;

namespace RampWreckers.Tests;

/// <summary>
/// Runs the real main scene for 60 wall-clock seconds and exits with a code.
/// Used by CI and tools/run_all_tests.sh so the "60-second boot" claim is literal.
/// </summary>
public partial class BootProbe : Node
{
    private GameRoot? _main;

    public override void _Ready()
    {
        Callable.From(() => { _ = Run(); }).CallDeferred();
    }

    private async Task Run()
    {
        _main = GD.Load<PackedScene>("res://scenes/Main.tscn").Instantiate<GameRoot>();
        AddChild(_main);
        ulong start = Time.GetTicksMsec();
        int frames = 0;
        while (Time.GetTicksMsec() - start < 60_000)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            frames++;
            if (frames % 3600 == 0)
                GD.Print($"[BOOT] {((Time.GetTicksMsec() - start) / 1000.0):0.0}s " +
                         $"frames={frames} vehicle={_main.CurrentVehicle != null} " +
                         $"statue={_main.CurrentStatue != null}");
        }

        bool ok = _main.CurrentVehicle != null && _main.CurrentStatue != null;
        GD.Print($"[BOOT] 60.0s completed: frames={frames}, " +
                 $"vehicle={_main.CurrentVehicle != null}, " +
                 $"statue={_main.CurrentStatue != null}, ok={ok}");
        GetTree().Quit(ok ? 0 : 1);
    }
}
