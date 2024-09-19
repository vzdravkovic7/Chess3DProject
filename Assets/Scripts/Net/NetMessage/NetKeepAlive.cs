using Unity.Collections;
using Unity.Networking.Transport;

public class NetKeepAlive : NetMessage
{
    public NetKeepAlive() { // <-- Making the box
        Code = OpCode.KEEP_ALIVE;
    }

    public NetKeepAlive(DataStreamReader reader) { // <-- Receiving the box
        Code = OpCode.KEEP_ALIVE;
        Deserialize(reader);
    }

    public override void Serialize(ref DataStreamWriter writer) {
        writer.WriteByte((byte)Code);
    }

    public override void Deserialize(DataStreamReader reader) {

    }

    public override void ReceivedOnClient() {
        NetUtility.C_KEEP_ALIVE?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn) {
        if (NetUtility.S_KEEP_ALIVE != null) {
            NetUtility.S_KEEP_ALIVE.Invoke(this, cnn);
        } else {
            UnityEngine.Debug.LogWarning("S_KEEP_ALIVE handler is not assigned.");
        }
    }
}
