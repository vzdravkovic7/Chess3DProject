using System;
using System.Collections.Generic;
using UnityEngine;
public class King : ChessPiece {
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY) {
        List<Vector2Int> r = new List<Vector2Int>();

        // Right
        if(currentX + 1 < tileCountX) {

            // Right
            if (board[currentX + 1, currentY] == null)
                r.Add(new Vector2Int(currentX + 1, currentY));
            else if (board[currentX + 1, currentY].team != team)
                r.Add(new Vector2Int(currentX + 1, currentY));

            // Top right
            if(currentY + 1 < tileCountY)
                if (board[currentX + 1, currentY + 1] == null)
                    r.Add(new Vector2Int(currentX + 1, currentY + 1));
                else if (board[currentX + 1, currentY + 1].team != team)
                    r.Add(new Vector2Int(currentX + 1, currentY + 1));

            // Bottom right
            if (currentY - 1 >= 0)
                if (board[currentX + 1, currentY - 1] == null)
                    r.Add(new Vector2Int(currentX + 1, currentY - 1));
                else if (board[currentX + 1, currentY - 1].team != team)
                    r.Add(new Vector2Int(currentX + 1, currentY - 1));
        }

        // Left

        if (currentX - 1 >= 0) {

            // Left
            if (board[currentX - 1, currentY] == null)
                r.Add(new Vector2Int(currentX - 1, currentY));
            else if (board[currentX - 1, currentY].team != team)
                r.Add(new Vector2Int(currentX - 1, currentY));

            // Top left
            if (currentY + 1 < tileCountY)
                if (board[currentX - 1, currentY + 1] == null)
                    r.Add(new Vector2Int(currentX - 1, currentY + 1));
                else if (board[currentX - 1, currentY + 1].team != team)
                    r.Add(new Vector2Int(currentX - 1, currentY + 1));

            // Bottom left
            if (currentY - 1 >= 0)
                if (board[currentX - 1, currentY - 1] == null)
                    r.Add(new Vector2Int(currentX - 1, currentY - 1));
                else if (board[currentX - 1, currentY - 1].team != team)
                    r.Add(new Vector2Int(currentX - 1, currentY - 1));
        }

        // Up
        if(currentY + 1 < tileCountY)
            if (board[currentX, currentY + 1] == null || board[currentX, currentY + 1].team != team)
                r.Add(new Vector2Int(currentX, currentY + 1));

        // Down
        if (currentY - 1 >= 0)
            if (board[currentX, currentY - 1] == null || board[currentX, currentY - 1].team != team)
                r.Add(new Vector2Int(currentX, currentY - 1));

        return r;
    }

    public override SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves, bool onReplay = false, int currentReplayMove = 0, bool goingForward = false, ChessPiece lastDeadPiece = null) {

        SpecialMove r = SpecialMove.None;

        Vector2Int[] kingMove = null;
        Vector2Int[] leftRook = null;
        Vector2Int[] rightRook = null;
        if (!onReplay) {
            kingMove = moveList.Find(m => m[0].x == 4 && m[0].y == ((team == 0) ? 0 : 7));
            leftRook = moveList.Find(m => m[0].x == 0 && m[0].y == ((team == 0) ? 0 : 7));
            rightRook = moveList.Find(m => m[0].x == 7 && m[0].y == ((team == 0) ? 0 : 7));
        } else {
            List<Vector2Int[]>  replayList = new List<Vector2Int[]>();

            if (!goingForward) {
                Vector2Int[] lastMove = moveList[currentReplayMove - 1];
                if (Math.Abs(lastMove[0].x - lastMove[1].x) == 2 && board[lastMove[1].x, lastMove[1].y].type == ChessPieceType.King) {
                    // If the king moved exactly 2 squares horizontally, it was a castling move
                    r = SpecialMove.Castling;
                    return r;
                }
            }

            for (int i = 0; i < currentReplayMove; i++) {
                Vector2Int[] move = moveList[i];
                // Create a new array to avoid referencing the same array in memory
                Vector2Int[] moveCopy = new Vector2Int[move.Length];
                move.CopyTo(moveCopy, 0);

                replayList.Add(moveCopy);
            }


            kingMove = replayList.Find(m => m[0].x == 4 && m[0].y == ((team == 0) ? 0 : 7));
            leftRook = replayList.Find(m => m[0].x == 0 && m[0].y == ((team == 0) ? 0 : 7));
            rightRook = replayList.Find(m => m[0].x == 7 && m[0].y == ((team == 0) ? 0 : 7));
        }

        if(kingMove == null && currentX == 4) {
            // White team
            if(team == 0) {
                // Left Rook
                if(leftRook == null)
                    if (board[0, 0].type == ChessPieceType.Rook)
                        if (board[0, 0].team == 0)
                            if (board[3, 0] == null)
                                if (board[2, 0] == null)
                                    if (board[1, 0] == null) {
                                        availableMoves.Add(new Vector2Int(2, 0));
                                        r = SpecialMove.Castling;
                                    }
                // Right Rook
                if (rightRook == null)
                    if (board[7, 0].type == ChessPieceType.Rook)
                        if (board[7, 0].team == 0)
                            if (board[5, 0] == null)
                                if (board[6, 0] == null) {
                                        availableMoves.Add(new Vector2Int(6, 0));
                                        r = SpecialMove.Castling;
                                    }
            } else {
                // Left Rook
                if (leftRook == null)
                    if (board[0, 7].type == ChessPieceType.Rook)
                        if (board[0, 7].team == 1)
                            if (board[3, 7] == null)
                                if (board[2, 7] == null)
                                    if (board[1, 7] == null) {
                                        availableMoves.Add(new Vector2Int(2, 7));
                                        r = SpecialMove.Castling;
                                    }
                // Right Rook
                if (rightRook == null)
                    if (board[7, 7].type == ChessPieceType.Rook)
                        if (board[7, 7].team == 1)
                            if (board[5, 7] == null)
                                if (board[6, 7] == null) {
                                    availableMoves.Add(new Vector2Int(6, 7));
                                    r = SpecialMove.Castling;
                                }
            }
        }

        return r;
    }
}
