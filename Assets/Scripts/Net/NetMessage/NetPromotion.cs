using Unity.Collections;
using Unity.Networking.Transport;

public class NetPromotion : NetMessage {
    public int teamId;
    public int promotionIndex;

    public NetPromotion() {
        Code = OpCode.PROMOTION;
    }

    public NetPromotion(DataStreamReader reader) {
        Code = OpCode.PROMOTION;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer) {
        writer.WriteByte((byte)Code);
        writer.WriteInt(teamId);
        writer.WriteInt(promotionIndex);
    }

    public override void Deserialize(DataStreamReader reader) {
        teamId = reader.ReadInt();
        promotionIndex = reader.ReadInt();
    }

    public override void ReceivedOnClient() {
        NetUtility.C_PROMOTION?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn) {
        NetUtility.S_PROMOTION.Invoke(this, cnn);
    }
}
