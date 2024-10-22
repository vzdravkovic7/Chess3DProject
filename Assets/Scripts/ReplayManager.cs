using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;

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

    public void LoadReplays() {

        string[] replayFiles = Directory.GetFiles(replayFolderPath, "*.json");
        replayDataList.Clear();

        foreach (string replayFile in replayFiles) {
            if (!loadedReplayFiles.Contains(replayFile)) {
                try {
                    string json = File.ReadAllText(replayFile);
                    ReplayData replayData = JsonUtility.FromJson<ReplayData>(json);

                    if (IsValidReplayData(replayData))
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

    public bool IsValidReplayData(ReplayData replayData) {
        if (replayData.moveList == null) {
            return false;
        }

        if (replayData.moveList.Count < 0) {
            return false;
        }

        // Validate all moves in moveList
        foreach (MoveData move in replayData.moveList) {
            if (move.start.x < 0 || move.start.x > 7 || move.start.y < 0 || move.start.y > 7 ||
                move.end.x < 0 || move.end.x > 7 || move.end.y < 0 || move.end.y > 7) {
                return false;
            }
        }

        // Check if chosenPieces, chosenPiecesPromotionPositions, and promotionMoveIndexList all have the same count
        if (replayData.chosenPieces.Count != replayData.chosenPiecesPromotionPositions.Count ||
            replayData.chosenPieces.Count != replayData.promotionMoveIndexList.Count) {
            return false; // The number of chosen pieces, promotion positions, and promotion indexes must match
        }

        // Check if each promotion position is within valid board range (0 to 7)
        foreach (var promotionPos in replayData.chosenPiecesPromotionPositions) {
            if (promotionPos.x < 0 || promotionPos.x > 7 || promotionPos.y < 0 || promotionPos.y > 7) {
                return false; // Invalid promotion position, out of bounds
            }
        }

        // Ensure promotionMoveIndexList values are within the valid range for the move list
        foreach (var promotionIndex in replayData.promotionMoveIndexList) {
            if (promotionIndex < 0 || promotionIndex >= replayData.moveList.Count) {
                return false; // Invalid promotion index, out of bounds of the move list
            }
        }

        // Check if startingTeam is valid (0 to 3)
        if (replayData.startingTeam < 0 || replayData.startingTeam > 1) {
            return false; // Invalid startingTeam
        }

        // Check if winningTeam is valid (0 to 3)
        if (replayData.winningTeam < 0 || replayData.winningTeam > 3) {
            return false; // Invalid winningTeam
        }

        // If all checks pass, the replay data is valid
        return true;
    }

}
