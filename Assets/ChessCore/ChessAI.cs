using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class Chess
{
    //importance rating per chess piece
    static readonly float[] ChessPieceValue =
    {
        1f, 2f, 3f, 4f, 10f, 100f
    };
    public bool exit = false;
    void PlayAITurn()
    {
        PieceMovement bestMove = GetBestMove(state, turnIndex, float.PositiveInfinity, float.PositiveInfinity, 4, out float points);
        state.MovePieces(bestMove);
        ProcessTurn();
    }

    /// <summary>
    /// this function uses the alpha-beta and minmax algorithm to search for the best move at the highest recursion depth.
    /// </summary>
    /// <param name="state">the board state to search in</param>
    /// <param name="playerIndex">the player to check the moves for</param>
    /// <param name="currentMax">the maximum of points this player may have. 
    /// when above this maximum, we'll abort the search, as the player that moved before this move will just do another move.</param>
    /// <param name="nextMax"></param>
    /// <param name="recursion">the amount of levels to search for. 0 = only examine the result of these moves, 1 = also examine the moves of the opposite player after this player moved</param>
    /// <param name="bestPointsAtLastRecursion">the amount of points we will have when we and the opponent move our best moves</param>
    /// <returns>the best move</returns>
    /// <exception cref="System.Exception"></exception>
    public PieceMovement GetBestMove(BoardState state, int playerIndex, float currentMax, float nextMax, float recursion, out float bestPointsAtLastRecursion)
    {
        if (exit)
            throw new System.Exception();
        float bestMovePoints = float.NegativeInfinity;
        ChessPlayer checkPlayer = state.players[playerIndex];
        List<PieceMovement> possibleMoves = state.GetAllPossibleMoves(checkPlayer);
        float[] singleMoveValues = new float[possibleMoves.Count];

        PieceMovement bestMove = new();
        int otherPlayer = 1 - playerIndex;

        int moveIndex = 0;
        //sort possible moves from best to worst
        foreach (PieceMovement m in possibleMoves)
        {
            //check how many points changed
            float pointsChange = 0;

            bool kingCaptured = false;
            if (m.pieceKilled != null)
            {
                pointsChange = ChessPieceValue[(int)m.pieceKilled.type];
                kingCaptured = m.pieceKilled.type == ChessPieceType.King;
                if (kingCaptured)
                {
                    //better than this doesn't exist
                    bestPointsAtLastRecursion = pointsChange;
                    return m;
                }
            }
            if (state.squares[m.from].type == ChessPieceType.Pawn)
            {
                if (checkPlayer.TransformY(m.to.y) == 7)
                {
                    pointsChange += ChessPieceValue[(int)ChessPieceType.Queen] - ChessPieceValue[(int)ChessPieceType.Pawn];
                }
            }
            singleMoveValues[moveIndex++] = pointsChange;
        }

        //traverse sorted tree
        //TODO: sort descending
        var sorted = singleMoveValues.Select((x, i) => new { singleMoveValue = x, originalIndex = i })
            .OrderByDescending(x => x.singleMoveValue);

        foreach (var option in sorted)
        {
            float fullMoveValue = option.singleMoveValue;
            PieceMovement m = possibleMoves[option.originalIndex];
            if (recursion > 0)
            {
                state.MovePieces(m);
                //swap alpha and beta
                PieceMovement oppositeMove = GetBestMove(state, otherPlayer, nextMax + option.singleMoveValue, currentMax - option.singleMoveValue, recursion - 1, out float otherPoints);
                state.RevertMove(m);
                fullMoveValue -= otherPoints;
            }
            if (fullMoveValue > bestMovePoints)
            {
                bestMovePoints = fullMoveValue;
                bestMove = m;
                if(fullMoveValue > currentMax)
                {
                    //this move is so good, that the player who 'moved' before will not do that move, so we can't do this move.
                    break;
                }
            }
            nextMax = Mathf.Min(nextMax, -fullMoveValue);
        }

        bestPointsAtLastRecursion = bestMovePoints;
        return bestMove;
    }
}
