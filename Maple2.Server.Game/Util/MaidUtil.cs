using Maple2.Database.Storage;
using Maple2.Model.Enum;
using Maple2.Model.Game;
using Maple2.Server.Game.Packets;
using Maple2.Server.Game.Session;

namespace Maple2.Server.Game.Util;

/// <summary>
/// Closeness and mood bookkeeping shared by maid dialogue and maid crafting.
/// </summary>
public static class MaidUtil {
    /// <summary>
    /// Mood is a 0-100 gauge. No table gives the cutoffs between the three LeadTime bands,
    /// so they are split evenly until a capture pins them down.
    /// </summary>
    public const int MoodMax = 100;
    private const int MoodGood = 34;
    private const int MoodVeryGood = 67;

    /// <summary>
    /// Adds closeness exp, carrying it into as many grades as it covers. MaidExpTable holds
    /// the exp needed to leave each grade and ConstantsTable caps the grade.
    /// </summary>
    public static void AddCloseness(GameSession session, Maid maid, int amount, long now) {
        if (amount == 0) {
            return;
        }

        maid.ClosenessExp = Math.Max(0, maid.ClosenessExp + amount);
        maid.ClosenessTime = now;

        int max = session.ServerTableMetadata.ConstantsTable.MaidAffinityMax;
        while (maid.ClosenessLevel < max
               && session.TableMetadata.MaidExpTable.Entries.TryGetValue((short) maid.ClosenessLevel, out long required)
               && required > 0
               && maid.ClosenessExp >= required) {
            maid.ClosenessExp -= (int) required;
            maid.ClosenessLevel++;
        }
    }

    public static void AddMood(Maid maid, int amount, long now) {
        if (amount == 0) {
            return;
        }

        maid.Mood = Math.Clamp(maid.Mood + amount, 0, MoodMax);
        maid.MoodTime = now;
    }

    /// <summary>
    /// Settles the mood a maid lost while nobody was talking to it. Mood is only read when
    /// something asks for it, so the decay is applied then instead of on a timer.
    /// </summary>
    public static void ApplyMoodDecay(GameSession session, Maid maid, long now) {
        int minutes = session.ServerTableMetadata.ConstantsTable.decreaseMaidMoodMinutes;
        int value = session.ServerTableMetadata.ConstantsTable.decreaseMaidMoodValue;
        if (minutes <= 0 || value <= 0 || maid.MoodTime <= 0 || now <= maid.MoodTime) {
            return;
        }

        long periods = (now - maid.MoodTime) / (minutes * 60L);
        if (periods <= 0) {
            return;
        }

        maid.Mood = (int) Math.Clamp(maid.Mood - periods * value, 0, MoodMax);
        maid.MoodTime += periods * minutes * 60L;
    }

    /// <summary>Which LeadTime band the maid's current mood falls in.</summary>
    public static MaidMood MoodTier(Maid maid) {
        return maid.Mood switch {
            >= MoodVeryGood => MaidMood.VeryGood,
            >= MoodGood => MaidMood.Good,
            _ => MaidMood.Normal,
        };
    }

    /// <summary>Pushes the maid's state to the client's maid list and to the field npc.</summary>
    public static void Refresh(GameSession session, Maid maid) {
        session.Send(MaidPacket.Change(maid));
        session.Send(MaidPacket.Field(maid));
        Save(session, maid);
    }

    /// <summary>
    /// Writes the maid back to its row. The field keeps the maid alive for as long as its
    /// contract cube stands, so this runs on every change rather than on a save tick.
    /// </summary>
    public static void Save(GameSession session, Maid maid) {
        using GameStorage.Request db = session.GameStorage.Context();
        db.SaveMaid(maid);
    }
}
