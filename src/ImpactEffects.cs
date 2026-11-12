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
    [Export] public int MaxActivePuffs { get; set; } = 22;
    [Export] public int MaxActiveChips { get; set; } = 28;
    [Export] public int MaxActiveFlashes { get; set; } = 6;
    [Export] public int MaxActiveSparks { get; set; } = 18;

    private int _activePuffs;
    private int _activeChips;
    private int _activeFlashes;
    private int _activeSparks;

    public int ActivePuffs => _activePuffs;
    public int ActiveChips => _activeChips;
    public int ActiveFlashes => _activeFlashes;
    public int ActiveSparks => _activeSparks;
    private ulong _randomState = 0x9e3779b97f4a7c15UL;

    public void SpawnDust(Vector3 position, float strength = 1f, int count = 3)
    {
        if (DustPuffScene == null)
            return;
        count = Mathf.Clamp(count, 1, 4);
        for (int i = 0; i < count; i++)
        {
            if (_activePuffs >= MaxActivePuffs)
                return;
            Node3D? puff = DustPuffScene.Instantiate<Node3D>();
            if (puff == null)
                return;
            AddChild(puff);
            puff.GlobalPosition = position + new Vector3(NextFloat(-0.38f, 0.38f),
                NextFloat(-0.12f, 0.35f), NextFloat(-0.38f, 0.38f)) * strength;
            float scale = NextFloat(0.62f, 1.02f) * Mathf.Clamp(strength, 0.45f, 1.55f);
            puff.Scale = Vector3.One * scale * 0.40f;
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
                        faded.AlbedoColor = new Color(0.72f, 0.64f, 0.51f, 0.30f);
                        faded.Roughness = 1f;
                        faded.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                        mesh.SetSurfaceOverrideMaterial(surface, faded);
                    }
                }
            }

            Vector3 drift = new(NextFloat(-0.75f, 0.75f), NextFloat(0.55f, 1.35f),
                NextFloat(-0.75f, 0.75f));
            Tween tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(puff, "global_position", puff.GlobalPosition + drift, 0.55f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(puff, "scale", Vector3.One * scale * 1.15f, 0.55f)
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
        count = Mathf.Clamp(count, 1, 8);
        for (int i = 0; i < count; i++)
        {
            if (_activeChips >= MaxActiveChips)
                return;
            Node3D? shard = DebrisShardScene.Instantiate<Node3D>();
            if (shard == null)
                return;
            AddChild(shard);
            shard.GlobalPosition = position + new Vector3(NextFloat(-0.3f, 0.3f),
                NextFloat(-0.15f, 0.4f), NextFloat(-0.3f, 0.3f));
            shard.Scale = Vector3.One * NextFloat(0.32f, 0.72f) * Mathf.Clamp(strength, 0.55f, 1.7f);
            _activeChips++;
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
                _activeChips = Mathf.Max(0, _activeChips - 1);
                if (GodotObject.IsInstanceValid(shard))
                    shard.QueueFree();
            }));
        }
    }

    /// <summary>Small bright chips for weak-point and collapse feedback.</summary>
    public void SpawnSparks(Vector3 position, Color color, int count = 3)
    {
        if (DebrisShardScene == null)
            return;
        count = Mathf.Clamp(count, 1, 6);
        for (int i = 0; i < count; i++)
        {
            if (_activeSparks >= MaxActiveSparks)
                return;
            Node3D? spark = DebrisShardScene.Instantiate<Node3D>();
            if (spark == null)
                return;
            AddChild(spark);
            spark.GlobalPosition = position + new Vector3(NextFloat(-0.35f, 0.35f),
                NextFloat(-0.1f, 0.45f), NextFloat(-0.35f, 0.35f));
            spark.Scale = Vector3.One * NextFloat(0.22f, 0.44f);
            foreach (Node node in spark.FindChildren("*", "MeshInstance3D", true, false))
            {
                if (node is not MeshInstance3D mesh)
                    continue;
                var material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Roughness = 1f,
                };
                mesh.MaterialOverride = material;
            }
            _activeSparks++;
            Vector3 throwVector = new(NextFloat(-2.2f, 2.2f), NextFloat(1.2f, 3.5f),
                NextFloat(-2.2f, 1.2f));
            Tween tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(spark, "global_position",
                spark.GlobalPosition + throwVector, 0.32f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(spark, "scale", Vector3.One * 0.02f, 0.32f);
            tween.Chain().TweenCallback(Callable.From(() =>
            {
                _activeSparks = Mathf.Max(0, _activeSparks - 1);
                if (GodotObject.IsInstanceValid(spark))
                    spark.QueueFree();
            }));
        }
    }

    /// <summary>Brief omni light at the contact point. It creates no mesh.</summary>
    public void SpawnFlash(Vector3 position, Color color, float energy = 2.4f)
    {
        if (_activeFlashes >= MaxActiveFlashes)
            return;
        var light = new OmniLight3D
        {
            Name = "ImpactFlash",
            LightColor = color,
            LightEnergy = energy,
            OmniRange = 3.6f,
            ShadowEnabled = false,
        };
        AddChild(light);
        light.GlobalPosition = position + Vector3.Up * 0.25f;
        _activeFlashes++;
        Tween tween = CreateTween();
        tween.TweenProperty(light, "light_energy", 0f, 0.16f)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() =>
        {
            _activeFlashes = Mathf.Max(0, _activeFlashes - 1);
            if (GodotObject.IsInstanceValid(light))
                light.QueueFree();
        }));
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
