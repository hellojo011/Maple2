using Maple2.Server.Game.Model;

namespace Maple2.Server.Game.Trigger;

/// <summary>
/// Compatibility shims for trigger scripts that depend on client mechanics the server
/// does not implement yet, and would otherwise deadlock. Each entry must name the missing
/// mechanic and the exact state it stalls in. Delete an entry once its mechanic works.
/// </summary>
public static class TriggerWorkarounds {
    /// <summary>
    /// 52000120 헤네시스 외곽 요새 (quest 50001551 헤네시스의 위기).
    ///
    /// During the wall phase the player is meant to lift the LiftUp_Bomb cubes that
    /// "set_cube trigger_ids=6000-6010" reveals and throw them at spawns 901-910. Those
    /// spawns sit ~4400 units west, behind the push barriers that 02/03_ShieldBarrier
    /// drive, so they cannot be reached on foot and no mob AI acquires a target that far
    /// out. Bombs are not implemented (Ms2TriggerCube keeps no position, and the map has
    /// no Liftable entities), so the mobs survive and Battle01Start07's
    /// "monster_dead 901-910" never passes.
    ///
    /// The script's own cinematic-skip path, PCVolunteer05CSkip, clears those spawns.
    /// Mirror that on the normal path so the mission stays completable.
    /// </summary>
    private static readonly int[] HenesysDefenseWallSpawns =
        [901, 902, 903, 904, 905, 906, 907, 908, 909, 910];

    public static void OnStateEnter(FieldTrigger trigger, string stateName) {
        if (trigger.Field.Metadata.XBlock is not "52000120_qd") {
            return;
        }

        if (!string.Equals(trigger.Value.Name, "01_HenesysDefense", StringComparison.OrdinalIgnoreCase)
            || stateName is not "PCVolunteer05Skip") {
            return;
        }

        foreach (FieldNpc npc in trigger.Field.EnumerateNpcs()) {
            if (HenesysDefenseWallSpawns.Contains(npc.SpawnPointId)) {
                trigger.Field.RemoveNpc(npc.ObjectId);
            }
        }
    }
}
