using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public partial class Chess
{
    //importance rating per chess piece
    static readonly float[] ChessPieceValue =
    {
        1f, 2f, 3f, 4f, 10f, 100f
    };
    void PlayAITurn()
    {
        PieceMovement bestMove = GetBestMove(state, aiPlayerIndex, 3, out float points);
        state.MovePieces(bestMove);

    }
    public List<PieceMovement> GetAllPossibleMoves(BoardState state, ChessPlayer player)
    {
        List<PieceMovement> moves = new List<PieceMovement>();
        foreach (ChessPiece piece in player.pieces)
        {
            moves.AddRange(state.GetPieceOptions(piece));
        }
        return moves;
    }
    public PieceMovement GetBestMove(BoardState state, int playerIndex, int recursion, out float points)
    {
        points = float.NegativeInfinity;
        ChessPlayer checkPlayer = state.players[playerIndex];
        List<PieceMovement> possibleMoves = GetAllPossibleMoves(state, checkPlayer);

        PieceMovement bestMove = new();
        int otherPlayer = 1 - playerIndex;

        foreach (PieceMovement m in possibleMoves)
        {
            //check how many points changed
            float pointsChange = 0;

            if (state.squares.TryGetValue(m.to, out ChessPiece killed))
            {
                pointsChange = ChessPieceValue[(int)killed.type];
            }
            if (state.squares[m.from].type == ChessPieceType.Pawn)
            {
                if(checkPlayer.TransformY(m.to.y) == 7)
                {
                    pointsChange += ChessPieceValue[(int)ChessPieceType.Queen] - ChessPieceValue[(int)ChessPieceType.Pawn];
                }
            }
            if(recursion > 0)
            {
                BoardState ifMoved = state.Clone();
                ifMoved.MovePieces(m);
                PieceMovement oppositeMove = GetBestMove(ifMoved, otherPlayer, recursion - 1, out float otherPoints);
                pointsChange -= otherPoints;
            }
            if(pointsChange > points)
            {
                points = pointsChange;
                bestMove = m;
            }
        }
        return bestMove;
    }
}
