using Unity.Collections;
using Unity.Networking.Transport;

public class NetSurrender : NetMessage {
    public int winningTeam;
    public int teamId;

    public NetSurrender() {
        Code = OpCode.SURRENDER;
    }

    public NetSurrender(DataStreamReader reader) {
        Code = OpCode.SURRENDER;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer) {
        writer.WriteByte((byte)Code);
        writer.WriteInt(winningTeam);
        writer.WriteInt(teamId);
    }

    public override void Deserialize(DataStreamReader reader) {
        winningTeam = reader.ReadInt();
        teamId = reader.ReadInt();
    }

    public override void ReceivedOnClient() {
        NetUtility.C_SURRENDER?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn) {
        NetUtility.S_SURRENDER.Invoke(this, cnn);
    }
}
