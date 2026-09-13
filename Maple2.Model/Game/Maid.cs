using Maple2.PacketLib.Tools;
using Maple2.Tools;

namespace Maple2.Model.Game;

/// <summary>
/// The maid block shared by the UserMaid and FieldMaid packets. The layout was resolved
/// with PacketStructureResolver and the field meanings confirmed against packet captures:
///
///   byte unknown, long id, long itemUid, long hireTime, long closenessTime, long accountId,
///   int maidId, int npcId, byte placed, int mood, int closenessLevel, int closenessExp,
///   long expiryTime, int n, string x13, (string x4) x n
/// </summary>
public class Maid : IByteSerializable {
    public const int FixedStringCount = 13;
    public const int StringsPerEntry = 4;

    /// <summary>Always 0 in every capture so far; meaning still unknown.</summary>
    public byte Unknown;
    /// <summary>Serial number of this maid within the account.</summary>
    public long Id;
    /// <summary>Uid of the contract item. FieldAddNpc repeats it to tie the npc to this maid.</summary>
    public long ItemUid;
    public long HireTime;
    /// <summary>Last time closeness changed; scripts read it as maid_affinity_time.</summary>
    public long ClosenessTime;
    public long AccountId;
    public int MaidId;
    public int NpcId;
    /// <summary>1 once the maid is standing in the field.</summary>
    public byte Placed;
    /// <summary>
    /// Mood, 0-100, shown as the profile's mood gauge. Dialogue moves it by the script
    /// function's MaidMoodIncrease and it decays by ConstantsTable.decreaseMaidMoodValue every
    /// decreaseMaidMoodMinutes; the band it falls in picks which LeadTime a recipe uses.
    /// </summary>
    public int Mood;
    /// <summary>Closeness grade, capped by ConstantsTable.MaidAffinityMax.</summary>
    public int ClosenessLevel;
    /// <summary>Closeness exp toward the next grade; MaidExpTable holds the thresholds.</summary>
    public int ClosenessExp;
    public long ExpiryTime;

    /// <summary>Craft this maid is working on, if any. Not part of the wire format.</summary>
    public MaidCraftItem? Craft;
    /// <summary>When mood decay was last settled. Not part of the wire format.</summary>
    public long MoodTime;
    /// <summary>
    /// Uid of the furnishing item the pad was placed from. The employment period lives on that
    /// item - it is the date the housing tool shows - so renewing has to move it there.
    /// Not part of the wire format.
    /// </summary>
    public long ContractItemUid;
    /// <summary>
    /// When salary was last paid. Renewal is gated on the expiry date rather than on this, so
    /// it is bookkeeping only.
    /// </summary>
    public long PayTime;

    public string[] Strings = new string[FixedStringCount];
    /// <summary>Trailing repeated block; each entry is <see cref="StringsPerEntry"/> strings.</summary>
    public IList<string[]> Entries = [];

    public void WriteTo(IByteWriter writer) {
        writer.WriteByte(Unknown);
        writer.WriteLong(Id);
        writer.WriteLong(ItemUid);
        writer.WriteLong(HireTime);
        writer.WriteLong(ClosenessTime);
        writer.WriteLong(AccountId);
        writer.WriteInt(MaidId);
        writer.WriteInt(NpcId);
        writer.WriteByte(Placed);
        writer.WriteInt(Mood);
        writer.WriteInt(ClosenessLevel);
        writer.WriteInt(ClosenessExp);
        writer.WriteLong(ExpiryTime);

        writer.WriteInt(Entries.Count);
        for (int i = 0; i < FixedStringCount; i++) {
            writer.WriteUnicodeString(Strings[i] ?? string.Empty);
        }

        foreach (string[] entry in Entries) {
            for (int i = 0; i < StringsPerEntry; i++) {
                writer.WriteUnicodeString(i < entry.Length ? entry[i] ?? string.Empty : string.Empty);
            }
        }
    }
}
