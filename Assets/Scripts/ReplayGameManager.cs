using UnityEngine;

public class ReplayGameManager : MonoBehaviour
{
    public static ReplayGameManager Instance { get; private set; }

    public void Awake() {
        Instance = this;
    }
}
