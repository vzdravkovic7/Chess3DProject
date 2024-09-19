using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class Client : MonoBehaviour {
    #region Singleton implementation
    public static Client Instance { get; set; }

    public void Awake() {
        Instance = this;
    }
    #endregion

    public NetworkDriver driver;
    private NetworkConnection connection;

    private bool isActive = false;

    public Action connectionDropped;

    // Methods
    public void Init(string ip, ushort port) {
        driver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(ip, port);

        connection = driver.Connect(endpoint);

        Debug.Log("Attempting to connect to Server on " + endpoint.Address);

        isActive = true;

        RegisterToEvent();
    }

    public void Shutdown() {
        if (!isActive) return;

        UnregisterToEvent();

        if (driver.IsCreated) {
            driver.Dispose();
        }

        isActive = false;
        connection = default(NetworkConnection);
    }

    public void OnDestroy() {
        Shutdown();
    }

    public void Update() {
        if (!isActive || !driver.IsCreated || !connection.IsCreated) {
            return;
        }

        driver.ScheduleUpdate().Complete();
        CheckAlive();
        UpdateMessagePump();
    }

    private void CheckAlive() {
        if (!connection.IsCreated && isActive) {
            Debug.LogWarning("Something went wrong, lost connection to server");
            connectionDropped?.Invoke();
        }
    }

    private void UpdateMessagePump() {
        if (!isActive || !driver.IsCreated || !connection.IsCreated) {
            return;
        }

        try {
            DataStreamReader stream;
            NetworkEvent.Type cmd;

            while ((cmd = connection.PopEvent(driver, out stream)) != NetworkEvent.Type.Empty) {
                if (cmd == NetworkEvent.Type.Connect) {
                    SendToServer(new NetWelcome());
                    Debug.Log("We're connected!");
                } else if (cmd == NetworkEvent.Type.Data) {
                    NetUtility.OnData(stream, default(NetworkConnection));
                } else if (cmd == NetworkEvent.Type.Disconnect) {
                    Debug.LogWarning("Client got disconnected from server");
                    connection = default(NetworkConnection);
                    connectionDropped?.Invoke();
                }
            }
        } catch (ObjectDisposedException e) {
            Debug.LogWarning($"Caught ObjectDisposedException: {e.Message}");
            // No shutdown here, just logging
        } catch (Exception e) {
            Debug.LogWarning($"Unexpected error: {e.Message}");
            // No shutdown here either, just logging
        }
    }

    public void SendToServer(NetMessage msg) {
        if (!isActive || !driver.IsCreated || !connection.IsCreated) {
            return;
        }

        DataStreamWriter writer;
        driver.BeginSend(connection, out writer);
        msg.Serialize(ref writer);
        driver.EndSend(writer);
    }

    // Event parsing
    private void RegisterToEvent() {
        NetUtility.C_KEEP_ALIVE += OnKeepAlive;
    }

    private void UnregisterToEvent() {
        NetUtility.C_KEEP_ALIVE -= OnKeepAlive;
    }

    private void OnKeepAlive(NetMessage nm) {
        // Send it back, to keep both sides alive
        SendToServer(nm);
    }
}
