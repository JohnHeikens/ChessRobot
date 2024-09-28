using UnityEngine;

public class ChessPiece
{
    public Vector2Int position;
    public ChessPieceType type;
    public GameObject gameObject;
    public ChessPlayer owner;
    public ChessPieceType lastRenderedType;
}
