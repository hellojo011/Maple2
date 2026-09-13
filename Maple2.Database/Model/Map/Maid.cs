using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maple2.Database.Model;

/// <summary>
/// A hired maid. The employment belongs to the account, not to the pad it stands on: picking
/// the pad up hands back a brand new cube item, so neither the cube uid nor the item uid
/// survives a round trip and the row is keyed by which maid the account hired. CubeUid only
/// records where it currently stands, which is how the client ties the npc to the maid.
/// </summary>
internal class Maid {
    public long Id { get; set; }
    public long AccountId { get; set; }
    public int MaidId { get; set; }
    public long CubeUid { get; set; }

    public long HireTime { get; set; }
    public long ExpiryTime { get; set; }
    public long PayTime { get; set; }

    public int ClosenessLevel { get; set; }
    public int ClosenessExp { get; set; }
    public long ClosenessTime { get; set; }

    public int Mood { get; set; }
    public long MoodTime { get; set; }

    public int CraftRecipeId { get; set; }
    public long CraftStartTime { get; set; }
    public int CraftLeadTime { get; set; }

    public static void Configure(EntityTypeBuilder<Maid> builder) {
        builder.ToTable("maid");
        builder.HasKey(maid => maid.Id);
        builder.Property(maid => maid.Id).ValueGeneratedOnAdd();
        // Not unique: two contracts for the same maid would share this row, but rejecting
        // that at the database is worse than letting the second pad rebind the same maid.
        builder.HasIndex(maid => new { maid.AccountId, maid.MaidId });
    }
}
