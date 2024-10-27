using UnityEngine;

public class PieceMovement
{
    public Vector2Int from;
    public Vector2Int to;
    /// <summary>
    /// the piece that was killed at the to location
    /// </summary>
    public ChessPiece pieceKilled;
    /// <summary>
    /// if the piece that did this move was a pawn and was promoted to queen
    /// </summary>
    public bool promoted;
    /// <summary>
    /// if the movement is a castling move. when executed, we also need to move the rook next to the king.
    /// </summary>
    public bool castling;
}
