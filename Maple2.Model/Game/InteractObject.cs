using System.Numerics;
using Maple2.Model.Enum;
using Maple2.Model.Metadata;
using Maple2.PacketLib.Tools;
using Maple2.Tools;

namespace Maple2.Model.Game;

public interface IInteractObject : IByteSerializable {
    public InteractType Type { get; }
    public string EntityId { get; }
    public int Id { get; }
}

public abstract class InteractObject<T> : IInteractObject where T : InteractObject {
    // Fallback implied by the placement entity kind (actor/mesh/display/...).
    protected abstract InteractType DefaultType { get; }

    // The behavior type comes from InteractObjectMetadata, not from the placement entity kind:
    // e.g. treasure chests are placed as Ms2InteractActor but are Mesh objects to the client.
    // Callers that have the metadata must set this; DefaultType is only the fallback.
    public InteractType Type { get; init; }

    protected T Metadata { get; init; }
    public int Id { get; init; }
    public string Model { get; init; } = "";
    public string Asset { get; init; } = "";
    public string NormalState { get; init; } = "";
    public string Reactable { get; init; } = "";
    public float Scale { get; init; } = 1f;

    public string EntityId { get; init; }

    protected InteractObject(string entityId, T metadata) {
        EntityId = entityId;
        Metadata = metadata;
        Id = metadata.InteractId;
        // Overridden by an object initializer when the caller has metadata.
        Type = DefaultType;
    }

    public virtual void WriteTo(IByteWriter writer) {
        writer.WriteString(EntityId);
        writer.Write<InteractState>(InteractState.Reactable);
        writer.Write<InteractType>(Type);
        writer.WriteInt(Id);
        writer.Write<Vector3>(Metadata.Position);
        writer.Write<Vector3>(Metadata.Rotation);
        writer.WriteUnicodeString(Model); // e.g. InteractMeshObject
        writer.WriteUnicodeString(Asset); // e.g. interaction_chestA_02
        writer.WriteUnicodeString(NormalState); // e.g. Opened_A
        writer.WriteUnicodeString(Reactable); // e.g. Idle_A
        writer.WriteFloat(Scale);
        writer.WriteBool(false);
    }
}

public sealed class InteractMeshObject(string entityId, Ms2InteractMesh metadata) : InteractObject<Ms2InteractMesh>(entityId, metadata) {
    protected override InteractType DefaultType => InteractType.Mesh;

}

public sealed class InteractTelescopeObject(string entityId, Ms2Telescope metadata) : InteractObject<Ms2Telescope>(entityId, metadata) {
    protected override InteractType DefaultType => InteractType.Telescope;

}

// sw_co_fi_funct_roulette_A01_
// co_fi_funct_roulette_A01_
// co_in_funct_extract_A01_
public sealed class InteractUiObject(string entityId, Ms2SimpleUiObject metadata) : InteractObject<Ms2SimpleUiObject>(entityId, metadata) {
    protected override InteractType DefaultType => InteractType.Ui;

}

// public sealed class InteractWebObject : InteractObject<InteractObject> {
//     protected override InteractType DefaultType => InteractType.Web;
//
//     public InteractWebObject(string entityId, InteractObject metadata) : base(entityId, metadata) { }
// }

public sealed class InteractDisplayImage(string entityId, Ms2InteractDisplay metadata) : InteractObject<Ms2InteractDisplay>(entityId, metadata) {
    protected override InteractType DefaultType => InteractType.DisplayImage;

}

public sealed class InteractGatheringObject(string entityId, Ms2InteractActor metadata) : InteractObject<Ms2InteractActor>(entityId, metadata) {
    protected override InteractType DefaultType => InteractType.Gathering;

    public int Count;

}

public sealed class InteractGuildPosterObject(string entityId, Ms2InteractDisplay metadata) : InteractObject<Ms2InteractDisplay>(entityId, metadata) {
    protected override InteractType DefaultType => InteractType.GuildPoster;

}

public sealed class InteractBillBoardObject : InteractObject<Ms2InteractMesh> {
    protected override InteractType DefaultType => InteractType.BillBoard;

    public long OwnerAccountId { get; init; }
    public long OwnerCharacterId { get; init; }
    public string OwnerName { get; init; }
    public string OwnerPicture { get; init; }
    public short OwnerLevel { get; init; }
    public JobCode OwnerJobCode { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool PublicHouse { get; init; }
    public long CreationTime { get; init; }
    public long ExpirationTime { get; init; }

    public InteractBillBoardObject(string entityId, Ms2InteractMesh metadata, Character owner) : base(entityId, metadata) {
        OwnerAccountId = owner.AccountId;
        OwnerCharacterId = owner.Id;
        OwnerName = owner.Name;
        OwnerPicture = owner.Picture;
        OwnerLevel = owner.Level;
        OwnerJobCode = owner.Job.Code();
    }

    public override void WriteTo(IByteWriter writer) {
        base.WriteTo(writer);
        writer.WriteLong(OwnerCharacterId);
        writer.WriteUnicodeString(OwnerName);
    }
}

// public sealed class InteractWatchTowerObject : InteractObject<InteractObject> {
//     protected override InteractType DefaultType => InteractType.WatchTower;
//
//     public InteractWatchTowerObject(string entityId, InteractObject metadata) : base(entityId, metadata) { }
// }
