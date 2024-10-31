using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [SerializeField] private Button fastBackwardButton;
    [SerializeField] private GameObject matchUI;
    [SerializeField] private TextMeshProUGUI warningText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject promotionPopup;
    [SerializeField] private GameObject inGameOptionsUI;
    [SerializeField] private Sprite disableRotation;
    [SerializeField] private Sprite enableRotation;
    [SerializeField] private Sprite disableVolume;
    [SerializeField] private Sprite enableVolume;

    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private Transform rematchIndicator;
    [SerializeField] private Button rematchButton;
    [SerializeField] private Button saveMatchButton;
    private Color originalColor;
    private float flashSpeed = 5f;
    private bool tickingStarted = false;
    private bool isScalingUp = false;
    private float promotionPopupSize = 1f;
    private bool promotionPopupActivate = false;
    private bool localRotateEnabled = true;

    public event EventHandler OnTickingTriggered;

    public void Awake() {
        Instance = this;
    }

    public void SetInitialSettings() {
        fastBackwardButton.transform.localScale = new Vector3(-1, 1, 1);
        originalColor = timerText.color;
    }

    public void UpdateTimer(float currentTime, bool isWhiteTurn) {
        timerText.text = (isWhiteTurn ? "WHITE" : "BLACK") + " TURN TIME LEFT: " + ((int)(currentTime / 60)).ToString("D2") + ":" + ((int)(currentTime % 60)).ToString("D2");

        if (currentTime < 10) {
            if (!tickingStarted && currentTime < 8) {
                OnTickingTriggered?.Invoke(this, EventArgs.Empty);
                tickingStarted = true;
            }

            float alpha = Mathf.Abs(Mathf.Sin(Time.time * flashSpeed));
            timerText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
        } else {
            timerText.color = originalColor;
            if (tickingStarted) {
                SoundManager.Instance.StopAudio();
                tickingStarted = false;
            }
        }
    }

    public void UpdatePromotionPopup() {
        Vector3 targetScale = isScalingUp ? new Vector3(1.4f, 1.4f, 1.4f) * promotionPopupSize : new Vector3(0.9f, 0.9f, 0.9f) * promotionPopupSize;
        int targetSpeed = isScalingUp ? 14 : 10;

        promotionPopup.transform.localScale = Vector3.Lerp(promotionPopup.transform.localScale, targetScale, Time.deltaTime * targetSpeed);

        if (Vector3.Distance(promotionPopup.transform.localScale, targetScale) < 0.01f) {
            promotionPopup.transform.localScale = targetScale;
            if (isScalingUp) isScalingUp = false;
            else promotionPopupActivate = false;
        }
    }

    public bool GetPromotionPopupActivate() {
        return promotionPopupActivate;
    }

    public void SetPromotionPopup(ChessPiece targetPawn) {
        promotionPopup.gameObject.SetActive(true);
        promotionPopupActivate = true;
        isScalingUp = true;
        promotionPopup.transform.localScale = Vector3.zero;
        Vector3 worldPosition = targetPawn.transform.position;
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
        screenPosition.y += 100;
        promotionPopup.transform.position = screenPosition;
    }

    public void RemovePromotionPopup() {
        promotionPopup.transform.localScale = Vector3.one * promotionPopupSize;
        promotionPopup.gameObject.SetActive(false);
        promotionPopupActivate = false;
        isScalingUp = false;
    }

    public void ToggleCameraRotation() {
        localRotateEnabled = !localRotateEnabled;
        if (localRotateEnabled)
            inGameOptionsUI.transform.GetChild(1).GetComponent<Button>().image.sprite = enableRotation;
        else inGameOptionsUI.transform.GetChild(1).GetComponent<Button>().image.sprite = disableRotation;
    }

    public bool GetLocalRotateEnabled() {
        return localRotateEnabled;
    }

    public void ToggleVolume() {
        if (SoundManager.Instance.ChangeVolumeEnabled())
            inGameOptionsUI.transform.GetChild(0).GetComponent<Button>().image.sprite = enableVolume;
        else inGameOptionsUI.transform.GetChild(0).GetComponent<Button>().image.sprite = disableVolume;
    }

    public void HandleDisplayVictory(int winningTeam) {
        victoryScreen.SetActive(true);
        if (winningTeam != 3)
            victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
        victoryScreen.transform.GetChild(5).GetComponent<Button>().interactable = true;
    }

    public void HandleRematchInfo(byte wantRematch) {
        rematchIndicator.transform.GetChild((wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
        if (wantRematch != 1) {
            rematchButton.interactable = false;
        }
    }

    public void SetRematchUI() {
        rematchButton.interactable = true;
        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);
    }

    public void SetSaveMatchUI() {
        saveMatchButton.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>().text = "SAVE MATCH";
        saveMatchButton.interactable = true;
    }

    public void DisableSaveMatchUI() {
        saveMatchButton.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>().text = "SAVED";
        saveMatchButton.interactable = false;
    }

    public void HandleGoingOnRematch() {
        warningText.gameObject.SetActive(true);
        timerText.gameObject.SetActive(true);
        matchUI.transform.GetChild(0).gameObject.SetActive(true);
        matchUI.gameObject.SetActive(true);
        inGameOptionsUI.transform.GetChild(0).gameObject.SetActive(true);
        inGameOptionsUI.transform.GetChild(1).gameObject.SetActive(true);
        inGameOptionsUI.gameObject.SetActive(true);
        ReplayManager.Instance.HandleReplayGoingOnRematch();
    }

    public void HandleGoingOnMenu() {
        matchUI.gameObject.SetActive(false);
        warningText.gameObject.SetActive(true);
        timerText.gameObject.SetActive(true);
        matchUI.transform.GetChild(0).gameObject.SetActive(true);
        inGameOptionsUI.gameObject.SetActive(false);
        inGameOptionsUI.transform.GetChild(0).gameObject.SetActive(true);
        inGameOptionsUI.transform.GetChild(1).gameObject.SetActive(true);
        ReplayManager.Instance.HandleReplayGoingOnMenu();
    }

    public void HandleGoingOnReplay() {
        warningText.gameObject.SetActive(false);
        timerText.gameObject.SetActive(false);
        matchUI.transform.GetChild(0).gameObject.SetActive(false);
        matchUI.gameObject.SetActive(false);
        inGameOptionsUI.transform.GetChild(0).gameObject.SetActive(false);
        inGameOptionsUI.transform.GetChild(1).gameObject.SetActive(false);
        inGameOptionsUI.gameObject.SetActive(false);
        ReplayManager.Instance.HandleGoingOnReplay();
    }

    public void HandleGameStart(float currentTime, bool localGame) {
        matchUI.gameObject.SetActive(true);
        timerText.text = "TIME LEFT: " + currentTime;
        if (localGame) inGameOptionsUI.gameObject.SetActive(true);
    }

    public void HandleGameWarning(string warning) {
        warningText.text = warning;
    }

    public void HandleCheckmate() {
        ReplayManager.Instance.HandleReplayCheckmate();
        matchUI.transform.GetChild(0).gameObject.SetActive(false);
        inGameOptionsUI.transform.GetChild(0).gameObject.SetActive(false);
        inGameOptionsUI.transform.GetChild(1).gameObject.SetActive(false);
    }

    public void HandleGameReset(bool onReplay) {
        if (!onReplay) {
            matchUI.transform.GetChild(0).gameObject.SetActive(true);
            inGameOptionsUI.transform.GetChild(0).gameObject.SetActive(true);
            inGameOptionsUI.transform.GetChild(1).gameObject.SetActive(true);

            SetSaveMatchUI();
        }
        matchUI.gameObject.SetActive(false);
        inGameOptionsUI.gameObject.SetActive(false);

        rematchButton.interactable = true;

        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);

        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(2).gameObject.SetActive(false);
        victoryScreen.SetActive(false);
    }

    public void HighlightTiles(ref List<Vector2Int> availableMoves, ref ChessPiece[,] chessPieces, ref GameObject[,] tiles, SpecialMove specialMove, int previousCount, bool isCheck, ChessPiece targetKing) {
        for (int i = 0; i < availableMoves.Count; i++)
            if (chessPieces[availableMoves[i].x, availableMoves[i].y] != null)
                tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("CaptureHighlight");
            else
                tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");

        if (specialMove == SpecialMove.Promotion)
            try {
                if (tiles[availableMoves[0].x, availableMoves[0].y].layer != LayerMask.NameToLayer("CaptureHighlight"))
                    tiles[availableMoves[0].x, availableMoves[0].y].layer = LayerMask.NameToLayer("SpecialHighlight");
            } catch (ArgumentOutOfRangeException) {

            }

        if (previousCount != availableMoves.Count) {
            for (int i = previousCount; i < availableMoves.Count; i++) {
                if (tiles[availableMoves[i].x, availableMoves[i].y].layer != LayerMask.NameToLayer("CaptureHighlight"))
                    tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("SpecialHighlight");
            }
        }

        if (isCheck) tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("KingCheck");
    }

    public void RemoveHighlightTiles(ref List<Vector2Int> availableMoves, ref ChessPiece[,] chessPieces, ref GameObject[,] tiles) {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");

        availableMoves.Clear();
    }

    public void RemoveCheckHighlight(ref GameObject[,] tiles, ChessPiece targetKing) {
        tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("Tile");
    }
}
