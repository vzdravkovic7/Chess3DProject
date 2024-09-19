using System;
using System.Net;
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
        if (isActive) {
            UnregisterToEvent();
            if (driver.IsCreated) {
                driver.Dispose();
            }
            isActive = false;
            connection = default(NetworkConnection);
        }
    }

    public void OnDestroy() {
        Shutdown();
    }

    public void Update() {
        if (!isActive || !driver.IsCreated) {
            return;
        }

        driver.ScheduleUpdate().Complete();
        CheckAlive();
        UpdateMessagePump();
    }

    private void CheckAlive() {
        if (!connection.IsCreated && isActive) {
            Debug.Log("Something went wrong, lost connection to server");
            connectionDropped?.Invoke();
            Shutdown();
        }
    }

    private void UpdateMessagePump() {
        if (!connection.IsCreated || !driver.IsCreated) {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = connection.PopEvent(driver, out stream)) != NetworkEvent.Type.Empty) {
            if (cmd == NetworkEvent.Type.Connect) {
                SendToServer(new NetWelcome());
                Debug.Log("We're connected!");
            } else if (cmd == NetworkEvent.Type.Data) {
                NetUtility.OnData(stream, default(NetworkConnection));
            } else if (cmd == NetworkEvent.Type.Disconnect) {
                Debug.Log("Client got disconnected from server");
                connection = default(NetworkConnection);
                connectionDropped?.Invoke();
                Shutdown();
            }
        }
    }

    private void SendToServer(NetMessage msg) {
        if (!driver.IsCreated || !connection.IsCreated) {
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