using Godot;

namespace RampWreckers;

/// <summary>
/// Managed visual effects. Dust and chips are instances of authored GLB meshes;
/// this class never creates a mesh or a primitive at runtime.
/// </summary>
public partial class ImpactEffects : Node3D
{
    [Export] public PackedScene DustPuffScene { get; set; } = null!;
    [Export] public PackedScene DebrisShardScene { get; set; } = null!;
    [Export] public int MaxActivePuffs { get; set; } = 36;

    private int _activePuffs;
    private ulong _randomState = 0x9e3779b97f4a7c15UL;

    public void SpawnDust(Vector3 position, float strength = 1f, int count = 3)
    {
        if (DustPuffScene == null)
            return;
        count = Mathf.Clamp(count, 1, 6);
        for (int i = 0; i < count; i++)
        {
            if (_activePuffs >= MaxActivePuffs)
                return;
            Node3D? puff = DustPuffScene.Instantiate<Node3D>();
            if (puff == null)
                return;
            AddChild(puff);
            puff.GlobalPosition = position + new Vector3(NextFloat(-0.7f, 0.7f),
                NextFloat(-0.25f, 0.55f), NextFloat(-0.7f, 0.7f)) * strength;
            float scale = NextFloat(0.55f, 1.05f) * Mathf.Clamp(strength, 0.45f, 1.8f);
            puff.Scale = Vector3.One * scale * 0.42f;
            _activePuffs++;

            foreach (Node node in puff.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (node is not MeshInstance3D mesh || mesh.Mesh == null)
                    continue;
                for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    Material? material = mesh.GetActiveMaterial(surface);
                    if (material is StandardMaterial3D original)
                    {
                        var faded = (StandardMaterial3D)original.Duplicate();
                        faded.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                        faded.AlbedoColor = new Color(0.90f, 0.84f, 0.72f, 0.28f);
                        faded.Roughness = 1f;
                        mesh.SetSurfaceOverrideMaterial(surface, faded);
                    }
                }
            }

            Vector3 drift = new(NextFloat(-1.6f, 1.6f), NextFloat(1.2f, 2.6f),
                NextFloat(-1.6f, 1.6f));
            Tween tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(puff, "global_position", puff.GlobalPosition + drift, 0.85f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(puff, "scale", Vector3.One * scale * 1.5f, 0.85f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                _activePuffs = Mathf.Max(0, _activePuffs - 1);
                if (GodotObject.IsInstanceValid(puff))
                    puff.QueueFree();
            }));
        }
    }

    public void SpawnChips(Vector3 position, float strength = 1f, int count = 2)
    {
        if (DebrisShardScene == null)
            return;
        count = Mathf.Clamp(count, 1, 5);
        for (int i = 0; i < count; i++)
        {
            Node3D? shard = DebrisShardScene.Instantiate<Node3D>();
            if (shard == null)
                return;
            AddChild(shard);
            shard.GlobalPosition = position + new Vector3(NextFloat(-0.5f, 0.5f),
                NextFloat(-0.2f, 0.6f), NextFloat(-0.5f, 0.5f));
            shard.Scale = Vector3.One * NextFloat(0.7f, 1.5f) * Mathf.Clamp(strength, 0.6f, 2f);
            Vector3 throwVector = new(NextFloat(-2.8f, 2.8f), NextFloat(2.2f, 5.2f),
                NextFloat(-2.8f, 1.4f));
            Tween tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(shard, "global_position",
                shard.GlobalPosition + throwVector, 0.55f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(shard, "rotation",
                shard.Rotation + new Vector3(NextFloat(-5f, 5f), NextFloat(-5f, 5f), NextFloat(-5f, 5f)),
                0.55f);
            tween.Chain().TweenProperty(shard, "position:y", shard.Position.Y - 3.2f, 0.4f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(shard))
                    shard.QueueFree();
            }));
        }
    }

    private float NextFloat(float min, float max)
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 7;
        _randomState ^= _randomState << 17;
        float unit = (_randomState & 0xFFFFFF) / (float)0x1000000;
        return min + (max - min) * unit;
    }
}
