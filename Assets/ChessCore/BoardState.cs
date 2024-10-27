using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using static Unity.VisualScripting.Member;
using static UnityEngine.UI.GridLayoutGroup;

public class BoardState
{
    public const int boardSize = 8;
    public const int playerCount = 2;
    //the board squares, ordered row major (a1, b1, c1 ... a2, b2, c2)
    public Dictionary<Vector2Int, ChessPiece> squares = new();
    //public ChessPiece[] squares = new ChessPiece[boardSize * boardSize];

    public ChessPlayer[] players = new ChessPlayer[playerCount];
    List<PieceMovement> history = new();
    public BoardState()
    {
        for (int i = 0; i < playerCount; i++)
        {
            players[i] = new ChessPlayer { id = i };
        }
    }
    public static bool Contains(Vector2Int position)
    {
        return position.x >= 0 && position.x < boardSize && position.y >= 0 && position.y < boardSize;
    }
    public bool TryMovingPiece(ChessPiece piece, Vector2Int destination)
    {
        if (Contains(destination))
        {
            var options = GetPieceOptions(piece);
            foreach (PieceMovement option in options)
            {
                if (option.to == destination)
                {
                    //this is possible
                    MovePieces(option);
                    return true;
                }
            }
        }
        return false;
    }
    public BoardState Clone()
    {
        BoardState b = new() { };
        for (int i = 0; i < playerCount; i++)
        {
            foreach (ChessPiece p in players[i].pieces)
            {
                ChessPiece clonedP = new ChessPiece { position = p.position, gameObject = p.gameObject, type = p.type, owner = b.players[i] };
                b.players[i].pieces.Add(clonedP);
                //we don't clone dead pieces
                b.squares.Add(p.position, clonedP);
            }
        }
        return b;
    }
    public bool Alive(ChessPiece p)
    {
        return squares.TryGetValue(p.position, out ChessPiece pieceAtDeathLocation) && pieceAtDeathLocation == p;
    }
    public PieceMovement GetCastleMovement(PieceMovement p)
    {
        int direction = Math.Sign(p.to.x - p.from.x);
        Vector2Int initialRookPosition = new(direction > 0 ? 7 : 0, p.to.y);
        Vector2Int finalRookPosition = new(p.to.x - direction, p.to.y);
        return new PieceMovement() { from = initialRookPosition, to = finalRookPosition };
    }
    public void MovePieces(PieceMovement p)
    {
        ChessPiece piece = squares[p.from];
        squares.Remove(p.from);
        if (p.pieceKilled != null)
        {
            p.pieceKilled.owner.pieces.Remove(p.pieceKilled);
            p.pieceKilled.owner.deadPieces.Add(p.pieceKilled);
        }
        squares[p.to] = piece;
        piece.position = p.to;
        if (p.promoted)
        {
            piece.type = ChessPieceType.Queen;
        }
        else if (p.castling)
        {
            MovePieces(GetCastleMovement(p));
        }
    }
    public void RevertMove(PieceMovement movement)
    {
        ChessPiece piece = squares[movement.to];
        squares.Remove(movement.to);
        if (movement.pieceKilled != null)
        {
            movement.pieceKilled.owner.deadPieces.Remove(movement.pieceKilled);
            movement.pieceKilled.owner.pieces.Add(movement.pieceKilled);
            squares.Add(movement.pieceKilled.position, movement.pieceKilled);
        }
        squares[movement.from] = piece;
        piece.position = movement.from;
        if (movement.promoted)
        {
            piece.type = ChessPieceType.Pawn;
        }
        else if (movement.castling)
        {
            RevertMove(GetCastleMovement(movement));
        }
    }

    public List<PieceMovement> GetPieceOptions(ChessPiece piece)
    {
        List<PieceMovement> options = GetPieceOptionsFast(piece);
        for (int i = options.Count - 1; i >= 0; i--)
        {
            //we can't make illegal moves
            if (CanKillKing(options[i]))
            {
                options.RemoveAt(i);
            }
        }
        return options;
    }

    /// <summary>
    /// this function doesn't check if the king is protected!
    /// </summary>
    /// <param name="piece"></param>
    /// <returns></returns>
    public List<PieceMovement> GetPieceOptionsFast(ChessPiece piece)
    {
        //for each piece, get the places it can go to

        List<PieceMovement> options = new();

        bool checkOption(Vector2Int pos, bool cantSlay = false, bool shouldSlay = false)
        {
            if (Contains(pos))
            {
                if (squares.TryGetValue(pos, out ChessPiece occupyingPiece) && (cantSlay || occupyingPiece.owner == piece.owner))
                    return false;
                if (!shouldSlay || occupyingPiece != null)
                {
                    options.Add(new PieceMovement
                    {
                        from = piece.position,
                        to = pos,
                        pieceKilled = occupyingPiece,
                        //reached the end of the board, transform to queen
                        promoted = (piece.type == ChessPieceType.Pawn) && (pos.y == piece.owner.TransformY(7))
                    });
                    return true;
                }

            }
            return false;
        }

        switch (piece.type)
        {
            case ChessPieceType.Pawn:
                {
                    int direction = piece.owner.id == 1 ? -1 : 1;
                    //either slay diagonally or walk forward
                    int stepCount = (piece.owner.id == 1 ? piece.position.y == 6 : piece.position.y == 1) ? 2 : 1;
                    Vector2Int checkPos = piece.position;
                    for (int i = 1; i <= stepCount; i++)
                    {
                        checkPos.y += direction;
                        if (!checkOption(checkPos, true)) break;
                    }

                    checkOption(piece.position + new Vector2Int(1, direction), false, true);
                    checkOption(piece.position + new Vector2Int(-1, direction), false, true);
                    break;
                }
            case ChessPieceType.Knight:
                //2 and 1
                for (int i = 0; i < 8; i++)
                {
                    //(1, 2), (2, 1), (2, -1), (1, -2)

                    Vector2Int off = new(1, 2);
                    if (i % 2 >= 1)
                    {
                        //swap
                        (off.x, off.y) = (off.y, off.x);
                    }
                    if (i % 4 >= 2)
                    {
                        off[1] = -off[1];
                    }
                    if (i >= 4)
                    {
                        off[0] = -off[0];
                    }
                    checkOption(piece.position + off);
                }
                break;
            case ChessPieceType.Rook:
                {
                    for (int axis = 0; axis < 2; axis++)
                    {
                        //go until we hit something
                        for (int direction = -1; direction <= 1; direction += 2)
                        {
                            for (int stepCount = 1; ; stepCount++)
                            {
                                Vector2Int off = new();
                                off[axis] = stepCount * direction;
                                if (!checkOption(piece.position + off) || squares.ContainsKey(piece.position + off))
                                {
                                    break;
                                }
                            }
                        }

                    }
                    break;
                }
            case ChessPieceType.Bishop:
            case ChessPieceType.Queen:
            case ChessPieceType.King:
                {
                    int step = piece.type == ChessPieceType.Bishop ? 2 : 1;
                    int maxStepCount = piece.type == ChessPieceType.King ? 1 : boardSize;
                    for (int dx = -1; dx <= 1; dx += step)
                    {
                        //go until we hit something
                        for (int dy = -1; dy <= 1; dy += step)
                        {
                            if (dx == 0 && dy == 0)
                            {
                                continue;
                            }
                            for (int stepCount = 1; stepCount <= maxStepCount; stepCount++)
                            {
                                Vector2Int off = new(dx * stepCount, dy * stepCount);
                                if (!checkOption(piece.position + off) || squares.ContainsKey(piece.position + off))
                                {
                                    break;
                                }
                            }
                        }

                    }
                    if (piece.type == ChessPieceType.King)
                    {
                        int startY = piece.owner.TransformY(0);
                        //we might be able to switch with a rook
                        //that only works if the king and rook are at their original position and nothing is between
                        if (piece.position.x == 4 && piece.position.y == startY)
                        {
                            //check for both rooks
                            for (int rookX = 0; rookX <= 7; rookX += 7)
                            {
                                if (squares.TryGetValue(new Vector2Int(rookX, startY), out ChessPiece rook) && rook.type == ChessPieceType.Rook)
                                {
                                    bool between = false;
                                    step = Math.Sign(rookX - piece.position.x);
                                    //check if all squares between are empty
                                    for (int checkX = piece.position.x + step; checkX != rookX; checkX += step)
                                    {
                                        if (squares.ContainsKey(new Vector2Int(checkX, startY)))
                                        {
                                            //something is here
                                            between = true;
                                            break;
                                        }
                                    }
                                    if (!between)
                                    {
                                        options.Add(new PieceMovement { from = piece.position, to = new Vector2Int(piece.position.x + step * 2, startY), castling = true });
                                    }
                                }
                            }
                        }
                    }
                    break;
                }
        }
        return options;
    }
    public List<PieceMovement> GetAllPossibleMoves(ChessPlayer player)
    {
        List<PieceMovement> moves = new List<PieceMovement>();
        foreach (ChessPiece piece in player.pieces)
        {
            moves.AddRange(GetPieceOptionsFast(piece));
        }
        return moves;
    }
    /// <summary>
    /// checks if the other player can kill the king when a certain move is made. when true, the move can't be executed.
    /// </summary>
    /// <param name="movement"></param>
    /// <returns></returns>
    public bool CanKillKing(PieceMovement movement)
    {
        ChessPlayer player = squares[movement.from].owner;
        MovePieces(movement);
        List<PieceMovement> moves = GetAllPossibleMoves(players[1 - player.id]);
        bool canKill = false;
        foreach (PieceMovement m in moves)
        {
            if (m.pieceKilled?.type == ChessPieceType.King)
            {
                canKill = true;
                break;
            }
        }
        RevertMove(movement);
        return canKill;
    }
}
