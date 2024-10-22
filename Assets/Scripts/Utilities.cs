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
}
