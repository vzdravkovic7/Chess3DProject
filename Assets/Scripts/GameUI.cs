using System;
using TMPro;
using UnityEngine;

public enum CameraAngle {
    menu = 0,
    whiteTeam = 1,
    blackTeam = 2
}

public class GameUI : MonoBehaviour
{
    public static GameUI Instance {  get; set; }

    public Server server;
    public Client client;

    [SerializeField] private Animator menuAnimator;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private GameObject[] cameraAngles;

    public Action<bool> SetLocalGame;
    public Action<ChessPieceType> PromotionSelected;

    public void Awake() {
        Instance = this;
        RegisterEvents();
    }

    // Cameras

    public void ChangeCamera(CameraAngle index) {
        for (int i = 0; i < cameraAngles.Length; i++)
            cameraAngles[i].SetActive(false);

        cameraAngles[(int)index].SetActive(true);
    }

    // Buttons

    public void OnLocalGameButton() {
        menuAnimator.SetTrigger("InGameMenu");
        SetLocalGame?.Invoke(true);
        server.Init(8007);
        client.Init("127.0.0.1", 8007);
    }

    public void OnOnlineGameButton() {
        menuAnimator.SetTrigger("OnlineMenu");
    }

    public void OnExitButton() {
        Application.Quit();
    }

    public void OnOnlineHostButton() {
        SetLocalGame?.Invoke(false);
        server.Init(8007);
        client.Init("127.0.0.1", 8007);
        menuAnimator.SetTrigger("HostMenu");
    }

    public void OnOnlineConnectButton() {
        if (addressInput.text != "") {
            SetLocalGame?.Invoke(false);
            client.Init(addressInput.text, 8007);
        }
    }

    public void OnOnlineBackButton() {
        menuAnimator.SetTrigger("StartMenu");
    }

    public void OnHostBackButton() {
        server.Shutdown();
        client.Shutdown();
        menuAnimator.SetTrigger("OnlineMenu");
    }

    public void OnLeaveFromGameMenu() {
        ChangeCamera(CameraAngle.menu);
        menuAnimator.SetTrigger("StartMenu");
    }

    public void OnSelectQueen() {
        PromotionSelected?.Invoke(ChessPieceType.Queen);
    }

    public void OnSelectRook() {
        PromotionSelected?.Invoke(ChessPieceType.Rook);
    }

    public void OnSelectBishop() {
        PromotionSelected?.Invoke(ChessPieceType.Bishop);
    }

    public void OnSelectKnight() {
        PromotionSelected?.Invoke(ChessPieceType.Knight);
    }

    #region
    private void RegisterEvents() {
        NetUtility.C_START_GAME += OnStartGameClient;
    }

    public void OnCameraRotationButton() {
        GameUIManager.Instance.ToggleCameraRotation();
    }

    public void OnVolumeButton() {
        GameUIManager.Instance.ToggleVolume();
    }

    public void OnSavedMatchesButton() {
        ReplayManager.Instance.EnableSavedMatchesUI();
    }

    public void OnSavedMatchesBackButton() {
        ReplayManager.Instance.DisableSavedMatchesUI();
    }

    private void UnRegisterEvents() {
        NetUtility.C_START_GAME -= OnStartGameClient;
    }

    private void OnStartGameClient(NetMessage message) {
        menuAnimator.SetTrigger("InGameMenu");
    }
    #endregion
}
