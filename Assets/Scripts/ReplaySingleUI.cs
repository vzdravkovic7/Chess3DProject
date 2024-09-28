using System.IO;
using TMPro;
using UnityEngine;

public class ReplaySingleUI : MonoBehaviour {
    [SerializeField] private TextMeshProUGUI replayInfoText;
    private ReplayData replayData;
    private string filePath;

    public void SetReplayData(ReplayData data, string replayFilePath) {
        replayData = data;
        filePath = replayFilePath;
        replayInfoText.text = replayData.saveDate;
        if(replayData.winningTeam != 0 && replayData.winningTeam != 1)
            replayInfoText.text += " - Winning Team: None";
        else
            replayInfoText.text += " - Winning Team: " + (replayData.winningTeam == 0 ? "White" : "Black");
        if (replayData.saveName != "") replayInfoText.text += " Name: " + replayData.saveName;
    }

    public void OnLoadReplay() {
        ReplayManager.Instance.SetReplayData(replayData);
    }

    public void OnDeleteReplay() {
        ReplayManager.Instance.DeleteReplay(replayData, filePath);

        Destroy(gameObject);
    }
}
