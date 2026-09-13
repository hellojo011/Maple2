namespace Maple2.Model.Metadata;

/// <summary>
/// A spot where a field treasure chest stands. The xblock marks these as region spawns named
/// "Chest_Normal_*" or "Chest_Rare_*" and nothing else in the client data says a chest belongs
/// there, so the entity name is the whole specification.
/// </summary>
public record FieldChestSpawn(bool Rare, Ms2RegionSpawn Spawn);
