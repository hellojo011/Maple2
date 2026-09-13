using Maple2.Model.Enum;
using Maple2.Model.Game;
using Maple2.Model.Metadata;
using Maple2.PacketLib.Tools;
using Maple2.Server.Core.Constants;
using Maple2.Server.Game.PacketHandlers.Field;
using Maple2.Server.Game.Packets;
using Maple2.Server.Game.Session;
using Maple2.Server.Game.Util;

namespace Maple2.Server.Game.PacketHandlers;

public class MaidCraftItemHandler : FieldPacketHandler {
    public override RecvOp OpCode => RecvOp.MaidCraftItem;

    private enum Command : byte {
        Request = 4,
        Collect = 6,
        InstantComplete = 7,
        Cancel = 10,
    }

    /// <summary>
    /// Seconds of remaining craft time one meret buys. The client quoted 13 merets for 30
    /// minutes left, which is ceil(1800 / 144); the real formula is not in any table.
    /// </summary>
    private const int SecondsPerMeret = 144;

    // Craft ids only have to be unique for the client while the session lasts.
    private static long craftUidCounter;

    public override void Handle(GameSession session, IByteReader packet) {
        var command = packet.Read<Command>();
        switch (command) {
            case Command.Request:
                HandleRequest(session, packet);
                return;
            case Command.Collect:
                HandleCollect(session, packet);
                return;
            case Command.InstantComplete:
                HandleInstantComplete(session, packet);
                return;
            case Command.Cancel:
                HandleCancel(session, packet);
                return;
            default:
                // Cancel and instant-complete are still unmapped; dump what the client sends
                // so the command byte and payload can be read off the log.
                byte[] payload = packet.ReadBytes(packet.Available);
                Logger.Debug("[MaidCraftItem] unmapped command {Command} ({CommandHex}), {Length} bytes: {Payload}",
                    (byte) command, $"0x{(byte) command:X2}", payload.Length, payload.ToHexString(payload.Length, ' '));
                return;
        }
    }

    private void HandleRequest(GameSession session, IByteReader packet) {
        int recipeId = packet.ReadInt();
        long maidItemUid = packet.ReadLong();

        Maid? maid = session.Field?.GetMaid(maidItemUid);
        if (maid is null) {
            Logger.Warning("Craft request for unknown maid {MaidItemUid}", maidItemUid);
            return;
        }

        if (maid.Craft is not null) {
            Logger.Debug("Maid {MaidId} is already crafting {RecipeId}", maid.MaidId, maid.Craft.RecipeId);
            return;
        }

        if (!session.ServerTableMetadata.MaidRecipeSvrTable.Entries.TryGetValue(recipeId, out MaidRecipeSvrTable.Entry? recipe)) {
            Logger.Warning("Maid recipe {RecipeId} not found", recipeId);
            return;
        }

        if (!KnowsRecipe(session, maid, recipeId)) {
            Logger.Debug("Maid {MaidId} cannot craft {RecipeId} at closeness {Level}", maid.MaidId, recipeId, maid.ClosenessLevel);
            return;
        }

        if (!ConsumeIngredients(session, recipe.Ingredients)) {
            Logger.Debug("Missing ingredients for maid recipe {RecipeId}", recipeId);
            return;
        }

        long start = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        MaidUtil.ApplyMoodDecay(session, maid, start);

        maid.Craft = new MaidCraftItem {
            Id = Interlocked.Increment(ref craftUidCounter),
            MaidItemUid = maidItemUid,
            RecipeId = recipeId,
            StartTime = start,
            LeadTime = LeadTime(maid, recipe),
        };
        maid.Craft.Remaining = maid.Craft.LeadTime;

        MaidUtil.Save(session, maid);

        session.Send(MaidPacket.AddCraft(maid.Craft));
        session.Send(MaidPacket.CraftAck(4, 1));
    }

    private void HandleCollect(GameSession session, IByteReader packet) {
        long maidItemUid = packet.ReadLong();

        Maid? maid = session.Field?.GetMaid(maidItemUid);
        if (maid?.Craft is null) {
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now < maid.Craft.StartTime + maid.Craft.LeadTime) {
            Logger.Debug("Craft {RecipeId} is not finished yet", maid.Craft.RecipeId);
            return;
        }

        Deliver(session, maid, now);
    }

    private void HandleInstantComplete(GameSession session, IByteReader packet) {
        long maidItemUid = packet.ReadLong();
        // The remaining 5 bytes of this request are not identified yet.

        Maid? maid = session.Field?.GetMaid(maidItemUid);
        if (maid?.Craft is null) {
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long remaining = maid.Craft.StartTime + maid.Craft.LeadTime - now;
        if (remaining > 0) {
            long cost = (remaining + SecondsPerMeret - 1) / SecondsPerMeret;
            if (session.Currency.Meret < cost) {
                Logger.Debug("Not enough merets to finish craft: need {Cost}", cost);
                return;
            }

            session.Currency.Meret -= cost;
        }

        Deliver(session, maid, now);
    }

    private void HandleCancel(GameSession session, IByteReader packet) {
        long maidItemUid = packet.ReadLong();

        Maid? maid = session.Field?.GetMaid(maidItemUid);
        if (maid?.Craft is null) {
            return;
        }

        // Cancelling does not return the ingredients, matching the client's warning.
        maid.Craft = null;
        MaidUtil.Save(session, maid);

        session.Send(MaidPacket.RemoveCraft(maidItemUid));
        session.Send(MaidPacket.CraftAck(6, 2));
    }

    private void Deliver(GameSession session, Maid maid, long now) {
        long maidItemUid = maid.ItemUid;

        if (!session.ServerTableMetadata.MaidRecipeSvrTable.Entries.TryGetValue(maid.Craft.RecipeId, out MaidRecipeSvrTable.Entry? recipe)) {
            maid.Craft = null;
            return;
        }

        // A jackpot upgrades the product and cheers the maid up.
        bool jackpot = Random.Shared.NextDouble() < recipe.JackpotRate;
        ItemComponent product = jackpot ? recipe.Jackpot : recipe.Product;

        Item? item = session.Field?.ItemDrop.CreateItem(product.ItemId, product.Rarity, product.Amount);
        if (item != null && !session.Item.Inventory.Add(item, true)) {
            session.Item.MailItem(item);
        }

        maid.Craft = null;
        MaidUtil.ApplyMoodDecay(session, maid, now);
        MaidUtil.AddCloseness(session, maid, recipe.MaidExp, now);
        if (jackpot) {
            MaidUtil.AddMood(maid, recipe.JackpotMood, now);
        }

        session.Send(MaidPacket.RemoveCraft(maidItemUid));
        session.Send(MaidPacket.CraftResult(product.ItemId, product.Amount, product.Rarity, recipe.MaidExp));
        session.Send(MaidPacket.CraftAck(6, 2));
        // The capture sent UserMaid mode 3 and FieldMaid back to back after a craft; the
        // profile reads the field copy, so both have to be refreshed.
        MaidUtil.Refresh(session, maid);
    }

    /// <summary>
    /// Recipe ingredients name either an item id or an ItemTag, and InventoryManager's
    /// component helper only resolves the id form, so match both here.
    /// </summary>
    private static bool ConsumeIngredients(GameSession session, IReadOnlyList<ItemComponent> ingredients) {
        lock (session.Item) {
            var plan = new List<(Item Item, int Amount)>();
            foreach (ItemComponent ingredient in ingredients) {
                IEnumerable<Item> candidates = ingredient.Tag != ItemTag.None
                    ? session.Item.Inventory.Find(ingredient.Tag)
                    : session.Item.Inventory.Find(ingredient.ItemId, ingredient.Rarity);

                int remaining = ingredient.Amount;
                foreach (Item item in candidates) {
                    if (remaining <= 0) {
                        break;
                    }

                    int take = Math.Min(item.Amount, remaining);
                    plan.Add((item, take));
                    remaining -= take;
                }

                if (remaining > 0) {
                    return false;
                }
            }

            foreach ((Item item, int amount) in plan) {
                session.Item.Inventory.Consume(item.Uid, amount);
            }

            return true;
        }
    }

    private static int LeadTime(Maid maid, MaidRecipeSvrTable.Entry recipe) {
        return MaidUtil.MoodTier(maid) switch {
            MaidMood.VeryGood => recipe.LeadTimeVeryGood,
            MaidMood.Good => recipe.LeadTimeGood,
            _ => recipe.LeadTimeNormal,
        };
    }

    private static bool KnowsRecipe(GameSession session, Maid maid, int recipeId) {
        if (!session.TableMetadata.MaidPropertyTable.Entries.TryGetValue(maid.MaidId, out MaidPropertyTable.Entry? property)) {
            return false;
        }

        if (!session.TableMetadata.MaidRecipeGroupTable.Entries.TryGetValue(property.RecipeGroupId, out MaidRecipeGroupTable.Entry? group)) {
            return false;
        }

        MaidRecipeGroupTable.Unlock? unlock = group.Recipes.FirstOrDefault(entry => entry.RecipeId == recipeId);
        return unlock != null && maid.ClosenessLevel >= unlock.RequireLevel;
    }
}
