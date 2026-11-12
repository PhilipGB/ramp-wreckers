using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RampWreckers;

namespace RampWreckers.UnitTests;

internal static class Program
{
    private static int _passed;
    private static int _failed;
    private static readonly List<string> Failures = new();

    private static void Check(bool condition, string description)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine("[PASS] " + description);
        }
        else
        {
            _failed++;
            Failures.Add(description);
            Console.WriteLine("[FAIL] " + description);
        }
    }

    private static int Main()
    {
        Console.WriteLine("=== RAMP WRECKERS CORE UNIT TESTS ===");
        TestCatalog();
        TestImpactModel();
        TestSaveSystem();
        Console.WriteLine($"=== RESULT: {_passed} passed, {_failed} failed ===");
        foreach (string failure in Failures)
            Console.WriteLine("FAILED: " + failure);
        return _failed == 0 ? 0 : 1;
    }

    private static void TestCatalog()
    {
        Check(ChallengeCatalog.All.Length == 9, "nine authored challenges exist");
        Check(ChallengeCatalog.TryValidate(out List<string> errors),
            "challenge part IDs and rewards validate (" + string.Join("; ", errors) + ")");
        Check(ChallengeCatalog.All.All(c => c.Reward > 0), "all challenges reward coins");
        Check(ChallengeCatalog.StatueCatalogHasAllParts(),
            "statue inventories contain required weak points");
        var twoParts = new HashSet<string> { "LeftLeg", "RightLeg" };
        Check(ChallengeCatalog.IsComplete(ChallengeCatalog.All[0], twoParts),
            "First Crash accepts any two broken parts");
        Check(!ChallengeCatalog.IsComplete(ChallengeCatalog.All[1],
                new HashSet<string> { "Beard" }), "Hat Trick rejects a non-hat part");
        Check(ChallengeCatalog.IsComplete(ChallengeCatalog.All[1],
                new HashSet<string> { "Hat" }), "Hat Trick accepts the hat");
    }

    private static void TestImpactModel()
    {
        Check(ImpactModel.Energy(0, 20) == 0f, "zero mass has no energy");
        Check(ImpactModel.Energy(1500, 20) > ImpactModel.Energy(1500, 10),
            "energy rises with speed");
        Check(ImpactModel.Energy(2600, 20) > ImpactModel.Energy(900, 20),
            "heavier vehicle has more energy at the same speed");
        Check(!ImpactModel.Breaks(89.9f, 90f), "below threshold is no break");
        Check(ImpactModel.Breaks(90f, 90f), "equal threshold breaks");
        Check(ImpactModel.Score(800, 60, true) > ImpactModel.Score(800, 60, false),
            "weak point score bonus applies");
        Check(ImpactModel.ShieldTransfer(400) < 400, "protection transfers only part of energy");
    }

    private static void TestSaveSystem()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ramp-wreckers-unit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "save.json");
            var store = new SaveStore(path);
            SaveData data = SaveData.CreateDefault();
            data.Coins = 4321;
            data.BestMedals[2] = 3;
            data.RewardClaimed[2] = true;
            data.VehiclesUnlocked[2] = true;
            data.EngineLevel = 1;
            Check(store.Save(data), "atomic save succeeds");
            SaveLoadResult loaded = store.Load();
            Check(loaded.LoadedFromDisk && loaded.Data.Coins == 4321 &&
                  loaded.Data.BestMedals[2] == 3 && loaded.Data.VehiclesUnlocked[2] &&
                  loaded.Data.EngineLevel == 1, "save roundtrip preserves progress and upgrades");

            SaveData second = SaveData.CreateDefault();
            second.Coins = 9999;
            Check(store.Save(second), "second save creates backup");
            File.WriteAllText(path, "{broken");
            SaveLoadResult recovered = store.Load();
            Check(recovered.RecoveredFromBackup && recovered.Data.Coins == 4321,
                "corrupt primary recovers last good backup");

            File.WriteAllText(path, string.Empty);
            if (File.Exists(store.BackupPath))
                File.Delete(store.BackupPath);
            SaveLoadResult empty = store.Load();
            Check(empty.HadProblem && empty.Data.Coins == 0,
                "empty save safely falls back to defaults");

            SaveData invalid = SaveData.CreateDefault();
            invalid.Format = "wrong";
            Check(!store.Save(invalid) && !string.IsNullOrEmpty(store.LastError),
                "invalid data is refused with an error message");

            string blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "file");
            var failing = new SaveStore(Path.Combine(blocker, "save.json"));
            Check(!failing.Save(SaveData.CreateDefault()) && failing.LastError.Length > 0,
                "write failure is reported honestly");

            var profile = new GameProfile(store);
            profile.Data.Coins = 1000;
            RewardResult first = profile.RecordChallenge(3, Medal.Bronze);
            RewardResult replay = profile.RecordChallenge(3, Medal.Gold);
            Check(first.FirstClear && first.Coins == ChallengeCatalog.All[3].Reward,
                "first clear grants the authored reward");
            Check(!replay.FirstClear && replay.Coins == 0 && profile.Data.BestMedals[3] == 3,
                "replay improves medal but cannot duplicate coins");
            Check(profile.Data.Coins == 1000 + ChallengeCatalog.All[3].Reward,
                "coins are exactly one reward");
            Check(profile.TryUnlockMonster().Success && profile.Data.VehiclesUnlocked[2],
                "monster truck unlock works with enough coins");
            Check(profile.TryUpgradeEngine().Success && profile.Data.EngineLevel == 1,
                "engine upgrade works with enough coins");
            Check(profile.TryUpgradeGrip().Success && profile.Data.GripLevel == 1,
                "grip upgrade works with enough coins");
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Temp cleanup is best effort.
            }
        }
    }
}
