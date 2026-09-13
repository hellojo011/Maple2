using Maple2.Model.Game;

namespace Maple2.Model.Metadata;

/// <summary>
/// Server/MaidGradeInfo.xml - per grade multipliers. The mood rates scale the crafting
/// lead time bracket the maid's mood puts it in.
/// </summary>
public record MaidGradeInfoTable(IReadOnlyDictionary<int, MaidGradeInfoTable.Entry> Entries) : ServerTable {
    public record Entry(
        int Grade,
        float JackpotRate,
        float MoodNormalRate,
        float MoodGoodRate,
        float MoodVeryGoodRate);
}

/// <summary>
/// Server/MaidRecipeSvr.xml - authoritative crafting data: ingredients, per mood lead
/// times, and the jackpot roll that doubles the product and raises the maid's mood.
/// </summary>
public record MaidRecipeSvrTable(IReadOnlyDictionary<int, MaidRecipeSvrTable.Entry> Entries) : ServerTable {
    public record Entry(
        int Id,
        IReadOnlyList<ItemComponent> Ingredients,
        int WorkbenchType,
        int LeadTimeNormal,
        int LeadTimeGood,
        int LeadTimeVeryGood,
        int MaidExp,
        ItemComponent Product,
        ItemComponent Jackpot,
        float JackpotRate,
        int JackpotMood,
        long ImmediatelyCompleteFee);
}
