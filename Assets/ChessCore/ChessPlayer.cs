using System.Collections.Generic;
using UnityEngine;

enum ChessPlayerColor
{
    white,
    black
}

public class ChessPlayer
{
    public int id = 0;
    public bool AI = false;
    public List<ChessPiece> pieces = new();
    public List<ChessPiece> deadPieces = new();
    /// <summary>
    /// will also convert inverse!
    /// </summary>
    /// <param name="relativeY">an y coordinate relative to the player</param>
    /// <returns>the board y</returns>
    public int TransformY(int relativeY)
    {
        return id * 7 + relativeY * (1 - id * 2);
    }
}
