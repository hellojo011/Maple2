using Maple2.PacketLib.Tools;
using Maple2.Server.Core.Constants;
using Maple2.Server.Core.PacketHandlers;
using Maple2.Server.Game.Session;

namespace Maple2.Server.Game.PacketHandlers;

/// <summary>
/// The maid feature is not implemented yet. PacketRouter drops opcodes that have no
/// handler without logging anything, so this exists purely to capture what the client
/// sends while the structure is being worked out. Replace the dump with real handling as
/// the commands are decoded.
/// </summary>
public class MaidHandler : PacketHandler<GameSession> {
    public override RecvOp OpCode => RecvOp.Maid;

    public override void Handle(GameSession session, IByteReader packet) {
        byte[] payload = packet.ReadBytes(packet.Available);
        Logger.Debug("[Maid 0x{OpCode:X4}] {Length} bytes: {Payload}",
            (ushort) OpCode, payload.Length, payload.ToHexString(payload.Length, ' '));
    }
}
