using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;

[System.Serializable]
public class MoveData {
    public Vector2Int start;
    public Vector2Int end;

    public MoveData(Vector2Int start, Vector2Int end) {
        this.start = start;
        this.end = end;
    }
}

[System.Serializable]
public class ReplayData {
    public int winningTeam;
    public int startingTeam;
    public List<MoveData> moveList;
    public List<ChessPieceType> chosenPieces;
    public List<Vector2Int> chosenPiecesPromotionPositions;
    public List<int> promotionMoveIndexList;
    public string saveDate;
    public string saveName;
}

public class ReplayManager : MonoBehaviour {
    private string replayFolderPath;

    [SerializeField] private GameObject replayPrefab; // Prefab to display replay info in the UI
    [SerializeField] private Transform savedMatchesUI; // Parent transform for the replay prefabs
    [SerializeField] private TMP_InputField saveNameInputField;
    [SerializeField] private GameObject replayUI;
    [SerializeField] private TextMeshProUGUI moveText;

    // Pagination
    private int currentPage = 0;
    private const int ITEMS_PER_PAGE = 5;
    private List<ReplayData> replayDataList = new List<ReplayData>();

    public event EventHandler<ReplayData> OnReplayLoaded;

    public static ReplayManager Instance;

    private HashSet<string> loadedReplayFiles = new HashSet<string>();
    private List<GameObject> instantiatedReplays = new List<GameObject>();

    private void Awake() {
        Instance = this;

        // Determine the folder where the replays will be saved
        replayFolderPath = Path.Combine(Application.dataPath, "..", "Replays");

        if (!Directory.Exists(replayFolderPath)) {
            Directory.CreateDirectory(replayFolderPath);
        }
    }

    public void SetReplayData(ReplayData loadedReplayData) {
        OnReplayLoaded?.Invoke(this, loadedReplayData);
    }
    public HashSet<string> GetLoadedReplayFiles() {
        return loadedReplayFiles;
    }

    public void SaveReplay(ReplayData replayData) {
        string filePath = Path.Combine(replayFolderPath, $"Replay_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");

        string json = JsonUtility.ToJson(replayData, true);

        File.WriteAllText(filePath, json);

        SetSaveNameText("");

        Debug.Log("Replay saved at: " + filePath);
    }

    public void OnRefreshButton() {
        ClearInstantiatedReplays();
        LoadReplays();
    }

    public void EnableSavedMatchesUI() {
        savedMatchesUI.gameObject.SetActive(true);
        LoadReplays();
    }

    public void DisableSavedMatchesUI() {
        savedMatchesUI.gameObject.SetActive(false);
    }

    public string GetSaveNameText() {
        return saveNameInputField.text;
    }

    public void SetSaveNameText(string text) {
        saveNameInputField.text = text;
    }

    public void SetMoveText(string text) {
        moveText.text = text;
    }

    public void LoadReplays() {

        string[] replayFiles = Directory.GetFiles(replayFolderPath, "*.json");
        replayDataList.Clear();

        foreach (string replayFile in replayFiles) {
            if (!loadedReplayFiles.Contains(replayFile)) {
                try {
                    string json = File.ReadAllText(replayFile);
                    ReplayData replayData = JsonUtility.FromJson<ReplayData>(json);

                    if (Utilities.Instance.IsValidReplayData(replayData))
                    {
                        replayDataList.Add(replayData);
                        loadedReplayFiles.Add(replayFile);
                    }
                    else
                    {
                        Debug.LogWarning("Invalid or corrupted replay data: " + replayFile);
                    }
                }
                catch (Exception ex)
                {
                    // JSON parsing errors, file read issues
                    Debug.LogWarning("Error loading replay: " + replayFile + ". Exception: " + ex.Message);
                }
            }
        }
        ShowPage(currentPage);
    }

    private void ShowPage(int page) {
        ClearInstantiatedReplays(); // Clear previous instances

        int startIndex = page * ITEMS_PER_PAGE;
        int endIndex = Mathf.Min(startIndex + ITEMS_PER_PAGE, replayDataList.Count);

        string replayFileName;
        for (int i = startIndex; i < endIndex; i++) {
            GameObject newReplayUI = Instantiate(replayPrefab, savedMatchesUI);

            replayFileName = Path.GetFileNameWithoutExtension(Directory.GetFiles(replayFolderPath, "*.json")[i]);
            newReplayUI.GetComponent<ReplaySingleUI>().SetReplayData(replayDataList[i], replayFileName);

            instantiatedReplays.Add(newReplayUI);
        }
    }

    private void ClearInstantiatedReplays() {
        foreach (GameObject replay in instantiatedReplays) {
            Destroy(replay);
        }
        instantiatedReplays.Clear();
        loadedReplayFiles.Clear();
    }

    public void OnNextPageButton() {
        if ((currentPage + 1) * ITEMS_PER_PAGE < replayDataList.Count) {
            currentPage++;
            ShowPage(currentPage);
        }
    }

    public void OnPreviousPageButton() {
        if (currentPage > 0) {
            currentPage--;
            ShowPage(currentPage);
        }
    }

    public void DeleteReplay(ReplayData replayData, string replayFileName) {
        string filePath = Path.Combine(replayFolderPath, $"{replayFileName}.json");

        if (loadedReplayFiles.Contains(filePath)) {
            loadedReplayFiles.Remove(filePath);
        }

        replayDataList.Remove(replayData);

        if (File.Exists(filePath)) {
            File.Delete(filePath);
            Debug.Log($"Replay file deleted: {filePath}");
        } else {
            Debug.LogWarning($"Attempted to delete replay file that does not exist: {filePath}");
        }

        ShowPage(currentPage);
    }

    // Helper method to set button interactability by index
    private void SetButtonsInteractable(bool isInteractable, params int[] buttonIndices) {
        foreach (int index in buttonIndices) {
            replayUI.transform.GetChild(index).gameObject.GetComponent<Button>().interactable = isInteractable;
        }
    }

    // Helper method to show or hide buttons by index
    private void SetButtonsActive(bool isActive, params int[] buttonIndices) {
        foreach (int index in buttonIndices) {
            replayUI.transform.GetChild(index).gameObject.SetActive(isActive);
        }
    }

    // Helper method to update move text based on the state
    private void UpdateMoveText(string newText) {
        moveText.text = newText.Trim();
    }

    public void HandleUIPause() {
        UpdateMoveText(moveText.text.Replace("(Press P to Pause)\n", "Paused\n"));
        // Re-enable replay buttons
        SetButtonsInteractable(true, 0, 1, 3, 4);
    }

    public void HandleUIFastForward() {
        SetButtonsInteractable(false, 0, 1);
    }

    public void HandleUIStopFastForward() {
        SetButtonsInteractable(true, 0);
        SetButtonsInteractable(false, 4);
        UpdateMoveText(moveText.text.Replace("(Press P to Pause)\n", ""));
    }

    public void HandleUIFastBackward() {
        SetButtonsInteractable(false, 3, 4);
    }

    public void HandleUIStopFastBackward() {
        SetButtonsInteractable(false, 0);
        SetButtonsInteractable(true, 3, 4);
        UpdateMoveText(moveText.text.Replace("(Press P to Pause)\n", ""));
    }

    public void HandleReplayCheckmate() {
        replayUI.gameObject.SetActive(false);
    }

    public void HandleReplayGoingOnRematch() {
        HandleReplayGoingOnMenu(); // Reuse the same logic
    }

    public void HandleReplayGoingOnMenu() {
        replayUI.gameObject.SetActive(false);
        SetButtonsActive(false, 0, 1, 3, 4);
        moveText.gameObject.SetActive(false);
    }

    public void HandleGoingOnReplay() {
        replayUI.gameObject.SetActive(true);
        SetButtonsActive(true, 0, 1, 3, 4);
        SetButtonsInteractable(false, 0, 1);
        SetButtonsInteractable(true, 3, 4);
        moveText.gameObject.SetActive(true);
        UpdateMoveText(""); // Clear the move text
    }

    public void EnableUIOnForward() {
        SetButtonsInteractable(true, 0, 1, 4);
    }

    public void ModifyMoveTextForward(bool isFastForwarding, string moveNotation) {
        string newText = isFastForwarding ? "(Press P to Pause)\n" + moveNotation : moveNotation;
        UpdateMoveText(newText);
    }

    public void DisableUIOnForward() {
        SetButtonsInteractable(false, 3, 4);
    }

    public void EnableUIOnBackward() {
        SetButtonsInteractable(true, 0, 3, 4);
    }

    public void ModifyMoveTextBackward(bool isFastReversing, string moveNotation) {
        string newText = isFastReversing ? "(Press P to Pause)\n" + moveNotation : moveNotation;
        UpdateMoveText(newText);

        if (!isFastReversing) {
            SoundManager.Instance.StopAudio();
            Utilities.Instance.TriggerSlidingSound();
        }
    }

    public void DisableUIOnBackward() {
        SetButtonsInteractable(false, 0, 1);
    }

}
