using Unity.Collections;
using Unity.Networking.Transport;

public class NetWelcome : NetMessage
{
    public int AssignedTeam {  get; set; }

    public NetWelcome() {
        Code = OpCode.WELCOME;
    }

    public NetWelcome(DataStreamReader reader) {
        Code = OpCode.WELCOME;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer) {
        writer.WriteByte((byte)Code);
        writer.WriteInt(AssignedTeam);
    }

    public override void Deserialize(DataStreamReader reader) {
        // We already read the byte in NetUtility::OnData
        AssignedTeam = reader.ReadInt();
    }

    public override void ReceivedOnClient() {
        NetUtility.C_WELCOME?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn) {
        NetUtility.S_WELCOME.Invoke(this, cnn);
    }
}
