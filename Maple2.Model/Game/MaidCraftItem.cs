using Maple2.PacketLib.Tools;
using Maple2.Tools;

namespace Maple2.Model.Game;

/// <summary>
/// One entry in a maid's crafting queue, as carried by the MaidCraftItem packet:
///   long id, long maidItemUid, int recipeId, long startTime, int leadTime, int remaining
/// </summary>
public class MaidCraftItem : IByteSerializable {
    public long Id;
    /// <summary>Uid of the contract item of the maid doing the work.</summary>
    public long MaidItemUid;
    public int RecipeId;
    public long StartTime;
    /// <summary>Seconds the craft takes; picked from the recipe by the maid's mood.</summary>
    public int LeadTime;
    public int Remaining;

    public void WriteTo(IByteWriter writer) {
        writer.WriteLong(Id);
        writer.WriteLong(MaidItemUid);
        writer.WriteInt(RecipeId);
        writer.WriteLong(StartTime);
        writer.WriteInt(LeadTime);
        writer.WriteInt(Remaining);
    }
}
