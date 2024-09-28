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
    bool kingCheck = false;
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
        BoardState b = new BoardState();
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
    public void MovePieces(PieceMovement p)
    {
        if (squares.TryGetValue(p.from, out ChessPiece piece))
        {
            squares.Remove(p.from);
            if (squares.TryGetValue(p.to, out ChessPiece pieceToKill))
            {
                pieceToKill.owner.pieces.Remove(pieceToKill);
                pieceToKill.owner.deadPieces.Add(pieceToKill);
            }
            squares[p.to] = piece;
            piece.position = p.to;
            if (piece.type == ChessPieceType.Pawn && p.to.y == piece.owner.TransformY(7))
            {
                //reached the end of the board, transform to queen
                piece.type = ChessPieceType.Queen;
            }
        }
    }
    public List<PieceMovement> GetPieceOptions(ChessPiece piece)
    {
        //for each piece, get the places it can go to

        List<PieceMovement> options = new();

        bool checkOption(Vector2Int pos, bool cantSlay = false, bool shouldSlay = false)
        {
            if (Contains(pos))
            {
                if (squares.TryGetValue(pos, out ChessPiece occupyingPiece) && (cantSlay || occupyingPiece.owner == piece.owner))
                    return false;
                if(!shouldSlay || occupyingPiece != null)
                {
                    options.Add(new PieceMovement { from = piece.position, to = pos });
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
                    for (int i = 1; i <= stepCount; i++)
                    {
                        checkOption(piece.position + new Vector2Int(0, direction * i), true);
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

                    Vector2Int off = new Vector2Int(1, 2);
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
                    break;
                }
        }
        return options;

    }
}
