using System.Text.RegularExpressions;
using Maple2.Database.Storage;
using Maple2.Model.Enum;
using Maple2.Model.Metadata;

namespace Maple2.Server.Game.Util;

/// <summary>
/// Every golden treasure chest in the game is its own interact object in the 14000000 range,
/// and the "Golden Chests of &lt;map&gt;" trophies are what say which ids belong to which map:
/// each names one map and covers exactly that map's chests as a code range. Within a map the
/// ids run in spawn point order, so the first "Chest_Rare_*" gets the range's low id.
/// </summary>
public static partial class FieldChestCodes {
    // The map a trophy is about only survives in its name, as the token the client resolves.
    [GeneratedRegex(@"\$map:(\d+)\$")]
    private static partial Regex MapToken();

    private const int FirstRareSpawnId = 3000;
    private const int CodeMin = 14000000;
    private const int CodeMax = 14999999;

    private static readonly object BuildLock = new();
    private static IReadOnlyDictionary<int, int>? codeBaseByMap;

    /// <summary>
    /// The interact object id for a golden chest, or 0 when the map has no trophy to take the
    /// ids from. Callers fall back to the generic golden chest, which drops the same loot but
    /// counts for no trophy.
    /// </summary>
    public static int Get(AchievementMetadataStorage achievements, int mapId, int spawnId) {
        IReadOnlyDictionary<int, int> bases = codeBaseByMap ?? Build(achievements);

        return bases.TryGetValue(mapId, out int codeBase) ? codeBase + (spawnId - FirstRareSpawnId) : 0;
    }

    private static IReadOnlyDictionary<int, int> Build(AchievementMetadataStorage achievements) {
        lock (BuildLock) {
            if (codeBaseByMap != null) {
                return codeBaseByMap;
            }

            var bases = new Dictionary<int, int>();
            foreach (AchievementMetadata achievement in achievements.GetType(ConditionType.interact_object)) {
                if (achievement.Name == null) {
                    continue;
                }

                Match map = MapToken().Match(achievement.Name);
                if (!map.Success || !int.TryParse(map.Groups[1].Value, out int mapId)) {
                    continue;
                }

                foreach (AchievementMetadataGrade grade in achievement.Grades.Values) {
                    if (grade.Condition.Codes?.Range is not { } range || range.Min < CodeMin || range.Max > CodeMax) {
                        continue;
                    }

                    bases[mapId] = range.Min;
                    break;
                }
            }

            codeBaseByMap = bases;

            return bases;
        }
    }
}
