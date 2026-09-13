using Maple2.Model.Game;
using Maple2.PacketLib.Tools;
using Maple2.Server.Core.Constants;
using Maple2.Server.Core.Packets;
using Maple2.Tools.Extensions;

namespace Maple2.Server.Game.Packets;

public static class MaidPacket {
    /// <summary>UserMaid: the account's maids. Only mode 0 has been resolved so far.</summary>
    public static ByteWriter Load(ICollection<Maid> maids) {
        var pWriter = Packet.Of(SendOp.UserMaid);
        pWriter.WriteByte();
        pWriter.WriteInt(maids.Count);
        foreach (Maid maid in maids) {
            pWriter.WriteClass<Maid>(maid);
        }

        return pWriter;
    }

    /// <summary>UserMaid mode 1: one maid was added or changed.</summary>
    public static ByteWriter Update(Maid maid) {
        var pWriter = Packet.Of(SendOp.UserMaid);
        pWriter.WriteByte(1);
        pWriter.WriteClass<Maid>(maid);

        return pWriter;
    }

    /// <summary>
    /// UserMaid mode 3: an existing maid changed. The capture used this after a craft
    /// raised closeness; mode 1 only registers a maid the client does not know yet.
    /// </summary>
    public static ByteWriter Change(Maid maid) {
        var pWriter = Packet.Of(SendOp.UserMaid);
        pWriter.WriteByte(3);
        pWriter.WriteClass<Maid>(maid);

        return pWriter;
    }

    /// <summary>MaidCraftItem mode 0: the whole crafting queue, sent on login.</summary>
    public static ByteWriter LoadCraft(ICollection<MaidCraftItem> items) {
        var pWriter = Packet.Of(SendOp.MaidCraftItem);
        pWriter.WriteByte();
        pWriter.WriteInt(items.Count);
        foreach (MaidCraftItem item in items) {
            pWriter.WriteClass<MaidCraftItem>(item);
        }

        return pWriter;
    }

    /// <summary>MaidCraftItem mode 1: a craft was queued.</summary>
    public static ByteWriter AddCraft(MaidCraftItem item) {
        var pWriter = Packet.Of(SendOp.MaidCraftItem);
        pWriter.WriteByte(1);
        pWriter.WriteClass<MaidCraftItem>(item);

        return pWriter;
    }

    /// <summary>MaidCraftItem mode 3: the maid's craft was taken off the queue.</summary>
    public static ByteWriter RemoveCraft(long maidItemUid) {
        var pWriter = Packet.Of(SendOp.MaidCraftItem);
        pWriter.WriteByte(3);
        pWriter.WriteLong(maidItemUid);

        return pWriter;
    }

    /// <summary>MaidCraftItem mode 11: what the craft produced.</summary>
    public static ByteWriter CraftResult(int itemId, int amount, int rarity, int closenessExp) {
        var pWriter = Packet.Of(SendOp.MaidCraftItem);
        pWriter.WriteByte(11);
        pWriter.WriteInt(itemId);
        pWriter.WriteInt(amount);
        pWriter.WriteInt(rarity);
        pWriter.WriteByte();
        pWriter.WriteInt(closenessExp);
        pWriter.WriteShort();

        return pWriter;
    }

    /// <summary>
    /// MaidCraftItem modes 4 and 6: the client gets one of these right after its craft
    /// request and its collect request. The int looked like a status code in the capture
    /// (1 after a request, 2 after a collect).
    /// </summary>
    public static ByteWriter CraftAck(byte mode, int code) {
        var pWriter = Packet.Of(SendOp.MaidCraftItem);
        pWriter.WriteByte(mode);
        pWriter.WriteInt(code);

        return pWriter;
    }

    /// <summary>FieldMaid: a single maid present in the field.</summary>
    public static ByteWriter Field(Maid maid) {
        var pWriter = Packet.Of(SendOp.FieldMaid);
        pWriter.WriteByte();
        pWriter.WriteClass<Maid>(maid);

        return pWriter;
    }
}
