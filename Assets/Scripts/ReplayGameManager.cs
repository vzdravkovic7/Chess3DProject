using System.Text.RegularExpressions;
using System;
using UnityEngine;
using System.Collections.Generic;

public class ReplayGameManager : MonoBehaviour
{
    public static ReplayGameManager Instance { get; private set; }

    [SerializeField] private Chessboard chessboard;

    private bool isFastForwarding = false;
    private bool isFastReversing = false;
    private float fastTimeCounter = 0f;
    private float fastMoveDelay = 0.2f; // Adjust speed of fast forward/backward
    private ChessPiece movePiece;
    private Vector2Int startPos;
    private Vector2Int endPos;
    private int currentReplayMove = 0;
    //private int currentReplayChosenPieceIndex = -1;
    private List<Vector2Int[]> moveList = null;
    private ChessPiece[,] chessPieces = null;
    private List<Vector2Int> availableMoves = null;
    private List<ChessPiece> deadWhites = null;
    private List<ChessPiece> deadBlacks = null;
    private List<ChessPieceType> chosenPieces = null;

    public void Awake() {
        Instance = this;
    }

    public void Start() {
        chessboard.OnReplayDataLoaded += Chessboard_OnReplayDataLoaded;
    }

    private void Chessboard_OnReplayDataLoaded(object sender, EventArgs e) {
        chessboard.ProvideGameState(
            ref moveList,
            ref chessPieces,
            ref availableMoves,
            ref deadWhites,
            ref deadBlacks,
            ref chosenPieces
        );

        currentReplayMove = 0;
    }

    public void HandleReplayControls() {
        if (Input.GetKeyDown(KeyCode.P)) {
            PauseReplay();
        }

        if (isFastForwarding) {
            ProcessFastForward();
        } else if (isFastReversing) {
            ProcessFastReverse();
        }
    }

    private void PauseReplay() {
        isFastForwarding = false;
        isFastReversing = false;

        ReplayManager.Instance.HandleUIPause();
    }

    private void ProcessFastForward() {
        ReplayManager.Instance.HandleUIFastForward();

        fastTimeCounter += Time.deltaTime;

        if (fastTimeCounter >= fastMoveDelay) {
            if (currentReplayMove < moveList.Count) // Ensure we don't go past the last move
            {
                OnForwardButton(); // Move forward one step
                fastTimeCounter = 0f; // Reset the counter
            } else {
                isFastForwarding = false; // Stop at the end of the list
                ReplayManager.Instance.HandleUIStopFastForward();
                OnForwardButton(); // additional call for checkmate if not normally happened
            }
        }
    }

    private void ProcessFastReverse() {
        ReplayManager.Instance.HandleUIFastBackward();

        fastTimeCounter += Time.deltaTime;

        if (fastTimeCounter >= fastMoveDelay) {
            if (currentReplayMove > 0) // Ensure we don't go before the first move
            {
                OnBackwardButton(); // Move backward one step
                fastTimeCounter = 0f; // Reset the counter
            } else {
                isFastReversing = false; // Stop at the start of the list
                ReplayManager.Instance.HandleUIStopFastBackward();
            }
        }
    }

    public void OnFastForwardButton() {
        isFastForwarding = true;
    }

    public bool GetIsFastForwarding() {
        return isFastForwarding;
    }

    public void OnForwardButton() {
        if (currentReplayMove < moveList.Count) {
            chessboard.SetGoingForward(true);
            if (!isFastForwarding) {
                ReplayManager.Instance.EnableUIOnForward();
            }

            ExecuteMoveForward();
        } else {
            HandleEndOfReplay();
        }
    }

    private void ExecuteMoveForward() {
        movePiece = chessPieces[moveList[currentReplayMove][0].x, moveList[currentReplayMove][0].y];
        startPos = new Vector2Int(moveList[currentReplayMove][0].x, moveList[currentReplayMove][0].y);
        endPos = new Vector2Int(moveList[currentReplayMove][1].x, moveList[currentReplayMove][1].y);

        ChessPiece cp = chessPieces[startPos.x, startPos.y];
        availableMoves = cp.GetAvailableMoves(ref chessPieces, Chessboard.TILE_COUNT_X, Chessboard.TILE_COUNT_Y);

        chessboard.specialMove = DetermineSpecialMove(cp);

        chessboard.MoveTo(startPos.x, startPos.y, endPos.x, endPos.y);

        string moveNotation = GenerateMoveNotation(chessboard.specialMove);

        ReplayManager.Instance.ModifyMoveTextForward(isFastForwarding, moveNotation);

        currentReplayMove++;
        chessboard.SetCurrentReplayMove(currentReplayMove);
    }

    private SpecialMove DetermineSpecialMove(ChessPiece cp) {
        if (cp.type == ChessPieceType.Pawn || cp.type == ChessPieceType.King) {
            return cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves, chessboard.GetOnReplay(), currentReplayMove, chessboard.GetGoingForward());
        } else {
            return cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
        }
    }

    private string GenerateMoveNotation(SpecialMove specialMove) {
        switch (specialMove) {
            case SpecialMove.Castling:
                return Utilities.Instance.CastlingNotation(endPos);
            case SpecialMove.Promotion:
                try {
                    return Utilities.Instance.GetMoveNotation(movePiece, startPos, endPos, chessboard.GetIsCapture(), chessboard.GetIsCheck(), chosenPieces[chessboard.GetCurrentReplayChosenPieceIndex()]);
                } catch (ArgumentOutOfRangeException) {
                    return "Surrendered during promotion!";
                }
            case SpecialMove.EnPassant:
                return Utilities.Instance.GetMoveNotation(movePiece, startPos, endPos, chessboard.GetIsCapture(), chessboard.GetIsCheck()) + "e.p.";
            default:
                return Utilities.Instance.GetMoveNotation(movePiece, startPos, endPos, chessboard.GetIsCapture(), chessboard.GetIsCheck());
        }
    }

    private void HandleEndOfReplay() {
        ReplayManager.Instance.DisableUIOnForward();
        chessboard.CheckMate(chessboard.GetWinningTeam());
    }

    public void OnFastBackwardButton() {
        isFastReversing = true;
    }

    public void OnBackwardButton() {
        if (currentReplayMove > 0) {
            chessboard.SetGoingForward(false);

            if (!isFastReversing) {
                ReplayManager.Instance.EnableUIOnBackward();
            }

            if (!ProcessMoveBackward()) {
                ReplayManager.Instance.SetMoveText("Spam or invalid move detected, closing replay...");
                chessboard.CheckMate(3);
                return;
            }

            HandleSpecialMoveBackward();

            currentReplayMove--;
            chessboard.SetCurrentReplayMove(currentReplayMove);
        } else {
            ReplayManager.Instance.DisableUIOnBackward();
        }
    }

    private bool ProcessMoveBackward() {
        try {
            movePiece = chessPieces[moveList[currentReplayMove - 1][1].x, moveList[currentReplayMove - 1][1].y];
            startPos = new Vector2Int(moveList[currentReplayMove - 1][0].x, moveList[currentReplayMove - 1][0].y);
            endPos = new Vector2Int(moveList[currentReplayMove - 1][1].x, moveList[currentReplayMove - 1][1].y);

            ChessPiece cp = chessPieces[endPos.x, endPos.y];
            availableMoves = cp.GetAvailableMoves(ref chessPieces, Chessboard.TILE_COUNT_X, Chessboard.TILE_COUNT_Y);
            chessboard.specialMove = DetermineSpecialMoveBackward(cp);

            chessboard.MoveTo(endPos.x, endPos.y, startPos.x, startPos.y);

            return true;
        } catch (NullReferenceException) {
            return false;
        }
    }

    private SpecialMove DetermineSpecialMoveBackward(ChessPiece cp) {
        if (cp.type == ChessPieceType.Pawn || cp.type == ChessPieceType.King) {
            if (cp.type == ChessPieceType.King || (deadWhites.Count == 0 && deadBlacks.Count == 0)) {
                return cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves, chessboard.GetOnReplay(), currentReplayMove, chessboard.GetGoingForward());
            } else {
                ChessPiece lastDeadPiece = GetLastDeadPiece(cp.team);
                return cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves, chessboard.GetOnReplay(), currentReplayMove, chessboard.GetGoingForward(), lastDeadPiece);
            }
        } else {
            return cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
        }
    }

    private ChessPiece GetLastDeadPiece(int team) {
        if ((deadBlacks.Count == 0 && !chessboard.GetIsWhiteTurn()) || (deadWhites.Count == 0 && chessboard.GetIsWhiteTurn())) {
            return null;  // No dead pieces, no previous capture
        } else {
            return (team == 0) ? deadBlacks[deadBlacks.Count - 1] : deadWhites[deadWhites.Count - 1];
        }
    }

    private void HandleSpecialMoveBackward() {
        if (chessboard.specialMove == SpecialMove.Promotion) {
            movePiece = chessPieces[moveList[currentReplayMove - 1][0].x, moveList[currentReplayMove - 1][0].y];
        }

        string moveNotation = Utilities.Instance.GetMoveNotation(movePiece, startPos, endPos, chessboard.GetIsCapture(), chessboard.GetIsCheck(), ChessPieceType.None, chessboard.GetGoingForward());
        ReplayManager.Instance.ModifyMoveTextBackward(isFastReversing, moveNotation);
    }

    public void ReverseEnPassant() {
        Vector2Int[] targetPawnPosition = moveList[currentReplayMove - 1];
        ChessPiece myPawn = chessPieces[targetPawnPosition[0].x, targetPawnPosition[0].y];
        ChessPiece enemyPawn = (myPawn.team == 0) ? deadBlacks[deadBlacks.Count - 1] : deadWhites[deadWhites.Count - 1];

        chessPieces[enemyPawn.currentX, enemyPawn.currentY] = enemyPawn;
        chessboard.PositionSinglePiece(enemyPawn.currentX, enemyPawn.currentY);
        enemyPawn.SetScale(Vector3.one);
        enemyPawn.capturedPosition = -Vector2Int.one;

        if (enemyPawn.team == 0) deadWhites.Remove(enemyPawn);
        else deadBlacks.Remove(enemyPawn);
    }
}
