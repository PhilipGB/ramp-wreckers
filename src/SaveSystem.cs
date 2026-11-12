using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RampWreckers;

/// <summary>Versioned save payload. All arrays are normalised on load.</summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;
    public const string Magic = "RampWreckersSave";

    public string Format { get; set; } = Magic;
    public int Version { get; set; } = CurrentVersion;
    public int Coins { get; set; }
    public bool[] VehiclesUnlocked { get; set; } = { true, true, false };
    public int[] BestMedals { get; set; } = new int[9];
    public bool[] RewardClaimed { get; set; } = new bool[9];
    public bool PaintUnlocked { get; set; }
    public int PaintIndex { get; set; }
    public int EngineLevel { get; set; }
    public int GripLevel { get; set; }
    public int CameraPreset { get; set; }
    public int QualityPreset { get; set; }
    public float MasterVolumeDb { get; set; }
    public bool ShowHelp { get; set; } = true;

    public static SaveData CreateDefault() => new();

    public SaveData Clone()
    {
        return new SaveData
        {
            Format = Format,
            Version = Version,
            Coins = Coins,
            VehiclesUnlocked = (bool[])VehiclesUnlocked.Clone(),
            BestMedals = (int[])BestMedals.Clone(),
            RewardClaimed = (bool[])RewardClaimed.Clone(),
            PaintUnlocked = PaintUnlocked,
            PaintIndex = PaintIndex,
            EngineLevel = EngineLevel,
            GripLevel = GripLevel,
            CameraPreset = CameraPreset,
            QualityPreset = QualityPreset,
            MasterVolumeDb = MasterVolumeDb,
            ShowHelp = ShowHelp,
        };
    }

    /// <summary>Returns false and a human-readable reason when the payload is unsafe.</summary>
    public bool ValidateAndNormalize(out string error)
    {
        if (!string.Equals(Format, Magic, StringComparison.Ordinal))
        {
            error = "Save file format marker is missing or unknown.";
            return false;
        }
        if (Version != CurrentVersion)
        {
            error = $"Unsupported save version {Version}; this build reads version {CurrentVersion}.";
            return false;
        }
        if (VehiclesUnlocked is null || VehiclesUnlocked.Length < 3)
        {
            error = "Save file has an invalid vehicle unlock list.";
            return false;
        }
        if (BestMedals is null || BestMedals.Length < 9 ||
            RewardClaimed is null || RewardClaimed.Length < 9)
        {
            error = "Save file has an invalid challenge progress list.";
            return false;
        }

        Coins = Math.Clamp(Coins, 0, 1_000_000);
        bool[] unlocked = (bool[])VehiclesUnlocked.Clone();
        Array.Resize(ref unlocked, 3);
        // The two starter vehicles can never be lost through a corrupt/malicious save.
        unlocked[0] = true;
        unlocked[1] = true;
        VehiclesUnlocked = unlocked;
        for (int i = 0; i < BestMedals.Length; i++)
            BestMedals[i] = Math.Clamp(BestMedals[i], 0, 3);
        PaintIndex = Math.Clamp(PaintIndex, 0, 5);
        EngineLevel = Math.Clamp(EngineLevel, 0, 1);
        GripLevel = Math.Clamp(GripLevel, 0, 1);
        CameraPreset = Math.Clamp(CameraPreset, 0, 2);
        QualityPreset = Math.Clamp(QualityPreset, 0, 2);
        MasterVolumeDb = Math.Clamp(MasterVolumeDb, -30f, 6f);
        error = string.Empty;
        return true;
    }

    public SaveData Sanitized()
    {
        SaveData copy = Clone();
        if (!copy.ValidateAndNormalize(out string error))
            throw new InvalidDataException(error);
        return copy;
    }
}

public sealed class SaveLoadResult
{
    public SaveData Data { get; init; } = SaveData.CreateDefault();
    public bool LoadedFromDisk { get; init; }
    public bool RecoveredFromBackup { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool HadProblem { get; init; }
}

/// <summary>
/// JSON persistence with an atomic temp-file replace and a validated backup.
/// A failed write never replaces the previous good save. Builds a SaveStore on any
/// path, so the corruption/failure tests can use a temporary directory.
/// </summary>
public sealed class SaveStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public SaveStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Save path is required.", nameof(path));
        Path = path;
        BackupPath = path + ".bak";
        TempPath = path + ".tmp";
    }

    public string Path { get; }
    public string BackupPath { get; }
    public string TempPath { get; }
    public string LastError { get; private set; } = string.Empty;

    public SaveLoadResult Load()
    {
        LastError = string.Empty;
        bool primaryExists = File.Exists(Path);
        bool backupExists = File.Exists(BackupPath);
        string primaryError = string.Empty;

        if (primaryExists)
        {
            if (TryRead(Path, out SaveData primary, out primaryError))
            {
                return new SaveLoadResult
                {
                    Data = primary,
                    LoadedFromDisk = true,
                    Message = "Progress loaded.",
                };
            }
        }

        if (backupExists && TryRead(BackupPath, out SaveData backup, out string backupError))
        {
            return new SaveLoadResult
            {
                Data = backup,
                LoadedFromDisk = true,
                RecoveredFromBackup = true,
                HadProblem = true,
                Message = "Main save was damaged; recovered the last good backup.",
            };
        }

        LastError = primaryExists
            ? (string.IsNullOrEmpty(primaryError) ? "Save data could not be read." : primaryError)
            : string.Empty;
        if (backupExists && string.IsNullOrEmpty(LastError))
            LastError = "Backup save data could not be read.";
        else if (backupExists)
            LastError += " Backup was also unusable.";

        return new SaveLoadResult
        {
            Data = SaveData.CreateDefault(),
            HadProblem = primaryExists || backupExists,
            Message = primaryExists || backupExists
                ? "Save files were unreadable. Started a safe new profile."
                : "New profile created.",
        };
    }

    /// <summary>Validates, writes to a temp file, then atomically replaces the save.</summary>
    public bool Save(SaveData data)
    {
        LastError = string.Empty;
        if (data is null)
        {
            LastError = "No save data was supplied.";
            return false;
        }

        SaveData copy = data.Clone();
        if (!copy.ValidateAndNormalize(out string validationError))
        {
            LastError = "Refused to write invalid save data: " + validationError;
            return false;
        }

        string directory = System.IO.Path.GetDirectoryName(Path) ?? ".";
        try
        {
            Directory.CreateDirectory(directory);
            string json = JsonSerializer.Serialize(copy, JsonOptions);

            // 1. Write a complete, flushed temp file.
            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write,
                       FileShare.None, 8192, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            // 2. Parse the temp file back before touching the live save.
            if (!TryRead(TempPath, out _, out string tempError))
            {
                LastError = "Save verification failed; previous save kept. " + tempError;
                SafeDelete(TempPath);
                return false;
            }

            // 3. Only back up a currently valid save.
            if (File.Exists(Path) && TryRead(Path, out _, out _))
            {
                string backupTemp = BackupPath + ".tmp";
                File.Copy(Path, backupTemp, overwrite: true);
                File.Move(backupTemp, BackupPath, overwrite: true);
            }

            // 4. Atomic replace on the same filesystem.
            File.Move(TempPath, Path, overwrite: true);

            // 5. Read back. If a bad disk truncated it, restore the backup.
            if (!TryRead(Path, out _, out string readbackError))
            {
                LastError = "Save read-back failed after writing. " + readbackError;
                if (File.Exists(BackupPath))
                {
                    try
                    {
                        File.Copy(BackupPath, Path, overwrite: true);
                    }
                    catch (Exception restoreError)
                    {
                        LastError += " Backup restore also failed: " + restoreError.Message;
                    }
                }
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LastError = "Could not save progress: " + ex.Message;
            SafeDelete(TempPath);
            return false;
        }
    }

    private static bool TryRead(string path, out SaveData data, out string error)
    {
        data = SaveData.CreateDefault();
        error = string.Empty;
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Save file is empty.";
                return false;
            }
            SaveData? parsed = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
            if (parsed is null)
            {
                error = "Save file contained no data.";
                return false;
            }
            if (!parsed.ValidateAndNormalize(out error))
                return false;
            data = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            error = "Save file is not valid JSON: " + ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = "Save file could not be read: " + ex.Message;
            return false;
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // A leftover temp file is harmless; the next save overwrites it.
        }
    }
}

/// <summary>In-memory profile plus the persistence policy for rewards and purchases.</summary>
public sealed class GameProfile
{
    public const int MonsterTruckCost = 600;
    public const int EngineUpgradeCost = 350;
    public const int GripUpgradeCost = 250;
    public const int PaintCost = 120;

    private readonly SaveStore _store;

    public GameProfile(SaveStore store)
    {
        _store = store;
        SaveLoadResult result = store.Load();
        Data = result.Data;
        LoadMessage = result.Message;
        LoadHadProblem = result.HadProblem;
        RecoveredFromBackup = result.RecoveredFromBackup;
    }

    public SaveData Data { get; private set; }
    public string LoadMessage { get; }
    public bool LoadHadProblem { get; }
    public bool RecoveredFromBackup { get; }
    public string LastSaveError { get; private set; } = string.Empty;

    public bool TrySave()
    {
        bool ok = _store.Save(Data);
        LastSaveError = ok ? string.Empty : _store.LastError;
        return ok;
    }

    /// <summary>
    /// Records a completion. Coins are granted exactly once per challenge, ever.
    /// Replaying can improve the stored medal but cannot mint more coins.
    /// </summary>
    public RewardResult RecordChallenge(int challengeIndex, Medal medal)
    {
        if (challengeIndex < 0 || challengeIndex >= ChallengeCatalog.All.Length)
            return new RewardResult(false, 0, medal, "Unknown challenge.");
        if (medal == Medal.None)
            return new RewardResult(false, 0, Medal.None, "Objective not complete.");

        WreckChallenge challenge = ChallengeCatalog.All[challengeIndex];
        bool firstClear = !Data.RewardClaimed[challengeIndex];
        int coins = 0;
        if (firstClear)
        {
            Data.RewardClaimed[challengeIndex] = true;
            coins = challenge.Reward;
            Data.Coins = Math.Min(1_000_000, Data.Coins + coins);
        }

        Medal previous = (Medal)Data.BestMedals[challengeIndex];
        bool medalImproved = medal > previous;
        if (medalImproved)
            Data.BestMedals[challengeIndex] = (int)medal;

        return new RewardResult(firstClear, coins, medal,
            firstClear ? $"Challenge complete! +{coins} Wreck Coins." :
            medalImproved ? $"New best medal: {ChallengeCatalog.MedalLabel(medal)}." :
            "Objective already rewarded — replay for a better medal.");
    }

    public PurchaseResult TryUnlockMonster()
    {
        if (Data.VehiclesUnlocked.Length > 2 && Data.VehiclesUnlocked[2])
            return new PurchaseResult(false, "Monster Truck is already unlocked.");
        if (Data.Coins < MonsterTruckCost)
            return new PurchaseResult(false, $"Need {MonsterTruckCost} Wreck Coins for the Monster Truck.");
        Data.Coins -= MonsterTruckCost;
        Data.VehiclesUnlocked[2] = true;
        return new PurchaseResult(true, "Monster Truck unlocked!");
    }

    public PurchaseResult TryUpgradeEngine()
    {
        if (Data.EngineLevel >= 1)
            return new PurchaseResult(false, "Engine is already upgraded.");
        if (Data.Coins < EngineUpgradeCost)
            return new PurchaseResult(false, $"Need {EngineUpgradeCost} Wreck Coins for the engine.");
        Data.Coins -= EngineUpgradeCost;
        Data.EngineLevel = 1;
        return new PurchaseResult(true, "Engine upgrade installed: faster acceleration and top speed.");
    }

    public PurchaseResult TryUpgradeGrip()
    {
        if (Data.GripLevel >= 1)
            return new PurchaseResult(false, "Grip is already upgraded.");
        if (Data.Coins < GripUpgradeCost)
            return new PurchaseResult(false, $"Need {GripUpgradeCost} Wreck Coins for the tyre upgrade.");
        Data.Coins -= GripUpgradeCost;
        Data.GripLevel = 1;
        return new PurchaseResult(true, "Tyre upgrade installed: more traction and steering.");
    }

    public PurchaseResult TryUnlockPaint()
    {
        if (Data.PaintUnlocked)
        {
            Data.PaintIndex = (Data.PaintIndex + 1) % 6;
            return new PurchaseResult(true, "Paint changed.");
        }
        if (Data.Coins < PaintCost)
            return new PurchaseResult(false, $"Need {PaintCost} Wreck Coins to unlock paint colours.");
        Data.Coins -= PaintCost;
        Data.PaintUnlocked = true;
        Data.PaintIndex = (Data.PaintIndex + 1) % 6;
        return new PurchaseResult(true, "Paint colours unlocked!");
    }
}

public readonly record struct RewardResult(bool FirstClear, int Coins, Medal Medal, string Message);
public readonly record struct PurchaseResult(bool Success, string Message);
