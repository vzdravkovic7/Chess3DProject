using UnityEngine;
using System.Collections.Generic;
using System;

public class SoundManager : MonoBehaviour {
    public AudioSource audioSource;
    public Chessboard chessboard;

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

        chessboard.OnMoveTriggered += Chessboard_OnMoveTriggered;
        chessboard.OnCaptureMoveTriggered += Chessboard_OnCaptureMoveTriggered;
        chessboard.OnEnPassantTriggered += Chessboard_OnEnPassantTriggered;
        chessboard.OnPromotionTriggered += Chessboard_OnPromotionTriggered;
        chessboard.OnCastlingTriggered += Chessboard_OnCastlingTriggered;
        chessboard.OnCheckTriggered += Chessboard_OnCheckTriggered;
        chessboard.OnVictoryTriggered += Chessboard_OnVictoryTriggered;
        chessboard.OnDefeatTriggered += Chessboard_OnDefeatTriggered;
        chessboard.OnStalemateTriggered += Chessboard_OnStalemateTriggered;
        chessboard.OnSlideTriggered += Chessboard_OnSlideTriggered;
        chessboard.OnTickingTriggered += Chessboard_OnTickingTriggered;
    }

    public bool ChangeVolumeEnabled() {
        isVolumeEnabled = !isVolumeEnabled;
        return isVolumeEnabled;
    }

    private void Chessboard_OnTickingTriggered(object sender, EventArgs e) {
        if(isVolumeEnabled) audioSource.PlayOneShot(tickingSound);
    }

    private void Chessboard_OnMoveTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(moveSound);
    }

    private void Chessboard_OnCaptureMoveTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(captureSound);
    }

    private void Chessboard_OnEnPassantTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(enPassantSound);
    }

    private void Chessboard_OnPromotionTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(promotionSound);
    }

    private void Chessboard_OnCastlingTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(castlingSound);
    }

    private void Chessboard_OnCheckTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(checkSound);
    }

    private void Chessboard_OnVictoryTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(victorySound);
    }

    private void Chessboard_OnDefeatTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(defeatSound);
    }

    private void Chessboard_OnStalemateTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(stalemateSound);
    }

    private void Chessboard_OnSlideTriggered(object sender, EventArgs e) {
        if (isVolumeEnabled) audioSource.PlayOneShot(slideSound);
    }
}
