using UnityEngine;
using System;

public class SoundManager : MonoBehaviour {
    [SerializeField] private AudioSource audioSource;

    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioClip moveSound;
    [SerializeField] private AudioClip captureSound;
    [SerializeField] private AudioClip castlingSound;
    [SerializeField] private AudioClip enPassantSound;
    [SerializeField] private AudioClip promotionSound;
    [SerializeField] private AudioClip checkSound;
    [SerializeField] private AudioClip slideSound;
    [SerializeField] private AudioClip stalemateSound;
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField] private AudioClip tickingSound;

    private bool isVolumeEnabled = true;

    private void Awake() {
        Instance = this;
    }

    void Start() {
        audioSource = GetComponent<AudioSource>();

        Utilities.Instance.OnMoveTriggered += Utilities_OnMoveTriggered;
        Utilities.Instance.OnCaptureMoveTriggered += Utilities_OnCaptureMoveTriggered;
        Utilities.Instance.OnEnPassantTriggered += Utilities_OnEnPassantTriggered;
        Utilities.Instance.OnPromotionTriggered += Utilities_OnPromotionTriggered;
        Utilities.Instance.OnCastlingTriggered += Utilities_OnCastlingTriggered;
        Utilities.Instance.OnCheckTriggered += Utilities_OnCheckTriggered;
        Utilities.Instance.OnSlideTriggered += Utilities_OnSlideTriggered;
        GameUIManager.Instance.OnTickingTriggered += GameUIManager_OnTickingTriggered;
    }

    public bool ChangeVolumeEnabled() {
        isVolumeEnabled = !isVolumeEnabled;
        return isVolumeEnabled;
    }

    public void StopAudio() {
        audioSource.Stop();
    }

    private void GameUIManager_OnTickingTriggered(object sender, EventArgs e) {
        if(isVolumeEnabled) audioSource.PlayOneShot(tickingSound);
    }

    private void Utilities_OnMoveTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(moveSound);
    }

    private void Utilities_OnCaptureMoveTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(captureSound);
    }

    private void Utilities_OnEnPassantTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(enPassantSound);
    }

    private void Utilities_OnPromotionTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(promotionSound);
    }

    private void Utilities_OnCastlingTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(castlingSound);
    }

    private void Utilities_OnCheckTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(checkSound);
    }

    private void Utilities_OnSlideTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(slideSound);
    }

    public void PlayEndGameSound(int winningTeam, int startingTeam) {
        if (isVolumeEnabled) {
            if (winningTeam == 2) audioSource.PlayOneShot(stalemateSound);
            else if (startingTeam != winningTeam) audioSource.PlayOneShot(defeatSound);
            else audioSource.PlayOneShot(victorySound);
        }
    }
}
