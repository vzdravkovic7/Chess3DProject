using System;
using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY) {
        List<Vector2Int> r = new List<Vector2Int>();
        int direction = (team == 0) ? 1 : -1;

        // One in front
        if (currentY + direction >= 0 && currentY + direction < tileCountY) {
            if (board[currentX, currentY + direction] == null)
                r.Add(new Vector2Int(currentX, currentY + direction));
        }

        // Two in front (only for first move)
        if (currentY + direction >= 0 && currentY + direction < tileCountY && board[currentX, currentY + direction] == null) {
            if (team == 0 && currentY == 1 && currentY + direction * 2 < tileCountY && board[currentX, currentY + direction * 2] == null)
                r.Add(new Vector2Int(currentX, currentY + direction * 2));

            if (team == 1 && currentY == 6 && currentY + direction * 2 >= 0 && board[currentX, currentY + direction * 2] == null)
                r.Add(new Vector2Int(currentX, currentY + direction * 2));
        }

        // Capture moves (diagonals)
        if (currentX + 1 < tileCountX && currentY + direction >= 0 && currentY + direction < tileCountY) {
            if (board[currentX + 1, currentY + direction] != null && board[currentX + 1, currentY + direction].team != team)
                r.Add(new Vector2Int(currentX + 1, currentY + direction));
        }

        if (currentX - 1 >= 0 && currentY + direction >= 0 && currentY + direction < tileCountY) {
            if (board[currentX - 1, currentY + direction] != null && board[currentX - 1, currentY + direction].team != team)
                r.Add(new Vector2Int(currentX - 1, currentY + direction));
        }

        return r;
    }

    public override SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves, bool onReplay = false, int currentReplayMove = 0, bool goingForward = false, ChessPiece lastDeadPiece = null) {
        int direction = (team == 0) ? 1 : -1;

        if ((team == 0 && currentY == 6) || (team == 1 && currentY == 1)) {
            if (onReplay) {
                if (goingForward)
                    return SpecialMove.Promotion;
            } else return SpecialMove.Promotion;
        }
        
        if(onReplay && currentReplayMove == 0) // Possible null reference in replay mode
            return SpecialMove.None;

        // En Passant
        if (moveList.Count > 0) {
            Vector2Int[] lastMove = null;
            if (!onReplay)
                lastMove = moveList[moveList.Count - 1];
            else if(goingForward)
                lastMove = moveList[currentReplayMove - 1];
            else {
                if (lastDeadPiece != null && lastDeadPiece.type == ChessPieceType.Pawn) {
                    lastMove = moveList[currentReplayMove - 2];
                    if (lastMove != null && Mathf.Abs(lastMove[1].y - lastMove[0].y) == 2) {
                        Vector2Int[] previousMove = moveList[currentReplayMove - 1];
                        if (previousMove[0].y == lastDeadPiece.capturedPosition.y) {
                            if (previousMove[0].x == lastDeadPiece.capturedPosition.x - 1 || previousMove[0].x == lastDeadPiece.capturedPosition.x + 1) { // The pawn was captured by en passant
                                if (lastDeadPiece.capturedPosition == lastMove[1] && lastDeadPiece.team != team)
                                    return SpecialMove.EnPassant;
                            }
                        }
                    }
                    return SpecialMove.None;
                } else lastMove = moveList[currentReplayMove - 1];

            }
            try {
                if (board[lastMove[1].x, lastMove[1].y].type == ChessPieceType.Pawn) { // If the last piece was a pawn
                    if (Mathf.Abs(lastMove[0].y - lastMove[1].y) == 2) { // If the last move was a +2 in either direction
                        if (board[lastMove[1].x, lastMove[1].y].team != team) { // If the move was from the other team
                            if (lastMove[1].y == currentY) { // If both pawns are on the same Y
                                if (lastMove[1].x == currentX - 1) { // Landed left
                                    availableMoves.Add(new Vector2Int(currentX - 1, currentY + direction));
                                    return SpecialMove.EnPassant;
                                }
                                if (lastMove[1].x == currentX + 1) { // Landed right
                                    availableMoves.Add(new Vector2Int(currentX + 1, currentY + direction));
                                    return SpecialMove.EnPassant;
                                }
                            }
                        }
                    }
                }
            } catch (NullReferenceException) {
                return SpecialMove.None;
            }
        }

        return SpecialMove.None;
    }
}
