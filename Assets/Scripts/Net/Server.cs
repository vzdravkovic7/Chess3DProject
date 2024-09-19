using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class Server : MonoBehaviour {
    #region Singleton implementation
    public static Server Instance { get; set; }

    public void Awake() {
        Instance = this;
    }
    #endregion

    public NetworkDriver driver;
    private NativeList<NetworkConnection> connections;

    private bool isActive = false;
    private const float keepAliveTickRate = 15.0f;
    private float lastKeepAlive;

    public Action connectionDropped;

    // Methods
    public void Init(ushort port) {
        driver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4;
        endpoint.Port = port;

        if (driver.Bind(endpoint) != 0) {
            Debug.LogWarning("Unable to bind on port " + endpoint.Port);
            return;
        } else {
            driver.Listen();
            Debug.Log("Currently listening on port " + endpoint.Port);
        }

        connections = new NativeList<NetworkConnection>(2, Allocator.Persistent);
        isActive = true;

        // Register the keep-alive event handler
        NetUtility.S_KEEP_ALIVE += OnKeepAliveReceived;
    }

    public void Shutdown() {
        if (!isActive) return;

        if (driver.IsCreated) {
            driver.Dispose();
        }
        if (connections.IsCreated) {
            connections.Dispose();
        }

        // Unregister the keep-alive event handler
        NetUtility.S_KEEP_ALIVE -= OnKeepAliveReceived;

        isActive = false;
    }

    public void OnDestroy() {
        Shutdown();
    }

    public void Update() {
        if (!isActive || !driver.IsCreated) {
            return;
        }

        KeepAlive();
        driver.ScheduleUpdate().Complete();

        CleanupConnections();
        AcceptNewConnections();
        UpdateMessagePump();
    }

    private void KeepAlive() {
        if (Time.time - lastKeepAlive > keepAliveTickRate) {
            lastKeepAlive = Time.time;
            Broadcast(new NetKeepAlive());
        }
    }

    private void CleanupConnections() {
        for (int i = connections.Length - 1; i >= 0; i--) {
            if (!connections[i].IsCreated) {
                connections.RemoveAtSwapBack(i);
            }
        }
    }

    private void AcceptNewConnections() {
        if (!driver.IsCreated) {
            return;
        }

        NetworkConnection c;
        while ((c = driver.Accept()) != default(NetworkConnection)) {
            connections.Add(c);
            Debug.Log($"Accepted new connection from {c}");
        }
    }

    private void UpdateMessagePump() {
        if (!driver.IsCreated) {
            return;
        }

        DataStreamReader stream;
        for (int i = 0; i < connections.Length; i++) {
            NetworkEvent.Type cmd;
            while ((cmd = driver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty) {
                switch (cmd) {
                    case NetworkEvent.Type.Data:
                        NetUtility.OnData(stream, connections[i], this);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        Debug.LogWarning($"Client {connections[i]} disconnected from server");
                        connections[i] = default(NetworkConnection);
                        connectionDropped?.Invoke();
                        break;
                }
            }
        }
    }

    // Server specific
    public void SendToClient(NetworkConnection connection, NetMessage msg) {
        if (!driver.IsCreated || !connection.IsCreated) {
            return;
        }

        DataStreamWriter writer;
        driver.BeginSend(connection, out writer);
        msg.Serialize(ref writer);
        driver.EndSend(writer);
    }

    public void Broadcast(NetMessage msg) {
        for (int i = 0; i < connections.Length; i++) {
            if (connections[i].IsCreated) {
                SendToClient(connections[i], msg);
            }
        }
    }

    // Keep-alive handler
    private void OnKeepAliveReceived(NetMessage msg, NetworkConnection cnn) {
        Debug.Log($"KeepAlive received from client: {cnn}");
        // Optionally respond back or update the server-side state
    }
}
