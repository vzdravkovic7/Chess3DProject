using System.Text.RegularExpressions;
using System;
using UnityEngine;

public class Utilities : MonoBehaviour
{
    public static Utilities Instance { get; private set; }

    [SerializeField] private Material[] pawnMaterials;
    [SerializeField] private Material[] knightMaterials;
    [SerializeField] private Material[] bishopMaterials;
    [SerializeField] private Material[] rookMaterials;
    [SerializeField] private Material[] queenMaterials;
    [SerializeField] private Material[] kingMaterials;


    public event EventHandler OnMoveTriggered;
    public event EventHandler OnCaptureMoveTriggered;
    public event EventHandler OnEnPassantTriggered;
    public event EventHandler OnPromotionTriggered;
    public event EventHandler OnCastlingTriggered;
    public event EventHandler OnCheckTriggered;
    public event EventHandler OnSlideTriggered;

    private void Awake() {
        Instance = this;
    }

    public string GetMoveNotation(ChessPiece piece, Vector2Int startPos, Vector2Int endPos, bool isCapture, bool isCheck, ChessPieceType promotedPieceType = ChessPieceType.None, bool goingForward = true) {
        string moveNotation = "";

        // Add the piece notation if not a pawn
        if (piece.type != ChessPieceType.Pawn) {
            moveNotation += GetPieceNotation(piece.type);
        }

        // Handle captures
        if (isCapture) {
            if (piece.type == ChessPieceType.Pawn) {
                // Pawn captures need to include the starting file
                if (goingForward)
                    moveNotation += (char)('a' + startPos.x);
                else moveNotation += (char)('a' + endPos.x);
            }
            moveNotation += "x";
        }

        // Add the destination square

        if(goingForward)
            moveNotation += ConvertToChessCoordinates(endPos);
        else moveNotation += ConvertToChessCoordinates(startPos);

        // Handle promotion
        if (promotedPieceType != ChessPieceType.None) {
            moveNotation += "=" + GetPieceNotation(promotedPieceType);
        }

        // Check or checkmate
        if (isCheck) {
            moveNotation += "+";
        }

        return moveNotation;
    }

    private string GetPieceNotation(ChessPieceType pieceType) {
        switch (pieceType) {
            case ChessPieceType.King: return "K";
            case ChessPieceType.Queen: return "Q";
            case ChessPieceType.Rook: return "R";
            case ChessPieceType.Bishop: return "B";
            case ChessPieceType.Knight: return "N";
            default: return ""; // Pawn has no letter
        }
    }

    private string ConvertToChessCoordinates(Vector2Int position) {
        char file = (char)('a' + position.x); // Convert x to 'a'-'h'
        char rank = (char)('1' + position.y); // Convert y to '1'-'8'
        return $"{file}{rank}";
    }

    public string CastlingNotation(Vector2Int endPos) {
        // Castling
        if (endPos.x == 6) // Kingside castling
            return "O-O";
        else if (endPos.x == 2) // Queenside castling
            return "O-O-O";

        return "";
    }

    public Material GetPieceMaterial(ChessPieceType type, int team) {
        switch (type) {
            case ChessPieceType.Pawn:
                return pawnMaterials[team];
            case ChessPieceType.Knight:
                return knightMaterials[team];
            case ChessPieceType.Bishop:
                return bishopMaterials[team];
            case ChessPieceType.Rook:
                return rookMaterials[team];
            case ChessPieceType.Queen:
                return queenMaterials[team];
            case ChessPieceType.King:
                return kingMaterials[team];
            default:
                return null;
        }
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

    public bool IsInsufficientMaterial(ref ChessPiece[,] chessPieces) {
        int whiteBishops = 0, whiteKnights = 0, blackBishops = 0, blackKnights = 0;
        bool whiteOtherPieces = false, blackOtherPieces = false;

        for (int x = 0; x < Chessboard.TILE_COUNT_X; x++) {
            for (int y = 0; y < Chessboard.TILE_COUNT_Y; y++) {
                if (chessPieces[x, y] != null) {
                    ChessPiece piece = chessPieces[x, y];
                    if (piece.type == ChessPieceType.Knight) {
                        if (piece.team == 0) whiteKnights++;
                        else blackKnights++;
                    } else if (piece.type == ChessPieceType.Bishop) {
                        if (piece.team == 0) whiteBishops++;
                        else blackBishops++;
                    } else if (piece.type != ChessPieceType.King) {
                        if (piece.team == 0) whiteOtherPieces = true;
                        else blackOtherPieces = true;
                    }
                }
            }
        }

        // Check insufficient material conditions
        if ((!whiteOtherPieces && !blackOtherPieces) && (
            (whiteKnights == 0 && blackKnights == 0 && whiteBishops <= 1 && blackBishops <= 1) ||
            (whiteKnights <= 1 && blackKnights <= 1 && whiteBishops == 0 && blackBishops == 0))) {
            return true;
        }

        return false;
    }

    public void PlayMoveSoundEffect(bool isCheck, SpecialMove specialMove, bool isCapture) {
        SoundManager.Instance.StopAudio();
        if (isCheck) OnCheckTriggered?.Invoke(this, EventArgs.Empty);
        else if (specialMove == SpecialMove.EnPassant) OnEnPassantTriggered?.Invoke(this, EventArgs.Empty);
        else if (specialMove == SpecialMove.Castling) OnCastlingTriggered?.Invoke(this, EventArgs.Empty);
        else if (isCapture) OnCaptureMoveTriggered?.Invoke(this, EventArgs.Empty);
        else OnMoveTriggered?.Invoke(this, EventArgs.Empty);
    }

    public void PlayReplaySoundEffect() {
        if (!ReplayGameManager.Instance.GetIsFastForwarding()) {
            SoundManager.Instance.StopAudio();
            TriggerSlidingSound();
        }
    }

    public void TriggerPromotionSound() {
        OnPromotionTriggered?.Invoke(this, EventArgs.Empty);
    }

    public void TriggerSlidingSound() {
        OnSlideTriggered?.Invoke(this, EventArgs.Empty);
    }
}
