using Maple2.Model.Game;

namespace Maple2.Model.Metadata;

/// <summary>maidproperty.xml - ties a maid to its npc and its recipe group.</summary>
public record MaidPropertyTable(IReadOnlyDictionary<int, MaidPropertyTable.Entry> Entries) : Table {
    public record Entry(
        int MaidId,
        int NpcId,
        int RecipeGroupId);
}

/// <summary>maidexp.xml - exp required to reach each maid level.</summary>
public record MaidExpTable(IReadOnlyDictionary<short, long> Entries) : Table;

/// <summary>&lt;language&gt;/maidsalary.xml - what the maid charges per pay period.</summary>
public record MaidSalaryTable(IReadOnlyDictionary<int, MaidSalaryTable.Entry> Entries) : Table {
    public record Entry(
        int MaidId,
        int SalaryType,
        long Amount);
}

/// <summary>maidrecipegroup.xml - which recipes a maid knows and the level each unlocks at.</summary>
public record MaidRecipeGroupTable(IReadOnlyDictionary<int, MaidRecipeGroupTable.Entry> Entries) : Table {
    public record Entry(
        int GroupId,
        IReadOnlyList<Unlock> Recipes);

    public record Unlock(
        int RecipeId,
        short RequireLevel);
}

/// <summary>
/// maidrecipe.xml - the client half of the recipe data. MaidRecipeSvrTable carries the
/// authoritative values; this only adds MaidMood, which the server table does not have.
/// </summary>
public record MaidRecipeTable(IReadOnlyDictionary<int, MaidRecipeTable.Entry> Entries) : Table {
    public record Entry(
        int Id,
        IReadOnlyList<ItemComponent> Ingredients,
        int WorkbenchType,
        int LeadTimeNormal,
        int LeadTimeGood,
        int LeadTimeVeryGood,
        int MaidExp,
        int MaidMood,
        ItemComponent Product);
}
