using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChessPieceType
{
    Pawn,
    Knight,
    Bishop,
    Rook,
    Queen,
    King
}


public partial class Chess : MonoBehaviour
{
    private static readonly int[] InitialPieceCountPerPlayer = { 8, 2, 2, 2, 1, 1 };
    private static readonly int ChessPieceTypeCount = Enum.GetValues(typeof(ChessPieceType)).Length;
    const int playerCount = 2;
    const int aiPlayerIndex = 0;
    const int realPlayerIndex = 1;
    BoardState state;
    public GameObject[] prefabs;
    Transform piecesContainer;
    public ChessPiece selectedPiece;
    public AudioClip moveSound;
    public AudioClip selectSound;
    AudioSource source;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        source = GetComponent<AudioSource>();
        state = new BoardState();
        //first, spawn all pieces.
        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            foreach (ChessPieceType type in Enum.GetValues(typeof(ChessPieceType)))
            {
                for (int i = 0; i < InitialPieceCountPerPlayer[(int)type]; i++)
                {
                    GameObject pieceObject = Instantiate(prefabs[(int)type + ChessPieceTypeCount * playerIndex], transform);
                    int relativeRowOffset = type == ChessPieceType.Pawn ? 1 : 0;
                    var colOffset = type switch
                    {
                        ChessPieceType.Rook => i == 1 ? 7 : 0,
                        ChessPieceType.Knight => i == 1 ? 6 : 1,
                        ChessPieceType.Bishop => i == 1 ? 5 : 2,
                        ChessPieceType.Queen => 4,
                        ChessPieceType.King => 3,
                        //pawn and default
                        _ => i,
                    };
                    int rowOffset = playerIndex == 1 ? 7 - relativeRowOffset : relativeRowOffset;
                    ChessPiece p = new() { gameObject = pieceObject, position = new Vector2Int(colOffset, rowOffset), type = type, lastRenderedType = type, owner = state.players[playerIndex] };
                    state.squares.Add(p.position, p);
                    state.players[playerIndex].pieces.Add(p);
                }
            }
        }

    }

    // Update is called once per frame
    void Update()
    {
        //visualise current board state

        const float maxInternalOffset = 3.5f;
        const float maxExternalOffset = 5.2f;
        Matrix4x4 internalToExternal = Matrix4x4.TRS(new Vector3(-maxExternalOffset, 0, -maxExternalOffset), Quaternion.identity, Vector3.one * maxExternalOffset / maxInternalOffset);

        //converting from internal board size to external board size

        for (int i = 0; i < state.players.Length; i++)
        {
            ChessPlayer player = state.players[i];
            foreach (ChessPiece piece in player.pieces)
            {
                if(piece.type != piece.lastRenderedType)
                {
                    Destroy(piece.gameObject);
                    piece.gameObject = Instantiate(prefabs[(int)piece.type + ChessPieceTypeCount * piece.owner.id], transform);

                }
                piece.gameObject.transform.localPosition = internalToExternal.MultiplyPoint3x4(new Vector3(piece.position.x, 0, piece.position.y));
            }
            int deadCount = 0;
            foreach (ChessPiece piece in player.deadPieces)
            {
                Vector2Int displayPosition = new Vector2Int(i == 1 ? BoardState.boardSize + 1 : -2, deadCount++);
                piece.gameObject.transform.localPosition = internalToExternal.MultiplyPoint3x4(new Vector3(displayPosition.x, 0, displayPosition.y));
            }
        }
        if (Input.GetMouseButtonDown(0))
        {
            //player can move objects
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit))
            {
                Transform objectHit = hit.transform;
                bool clickUsed = false;
                // Do something with the object that was hit by the raycast.
                foreach (ChessPiece p in state.players[realPlayerIndex].pieces)
                {
                    if (p.gameObject == objectHit.gameObject)
                    {
                        selectedPiece = p;
                        source.PlayOneShot(selectSound);
                        clickUsed = true;
                        break;
                    }
                }
                if (!clickUsed)
                {
                    foreach (ChessPiece p in state.players[aiPlayerIndex].pieces)
                    {
                        if (p.gameObject == objectHit.gameObject)
                        {
                            //try attacking this pawn
                            clickUsed = true;
                            TryMoveToTargetPos(p.position);
                            break;
                        }
                    }
                }
                if (objectHit == transform)
                {
                    if (selectedPiece != null)
                    {
                        //move pawn

                        //get destination position

                        Vector3 worldDest = hit.point;
                        Vector3 exactInternalBoardDest = internalToExternal.inverse.MultiplyPoint3x4(transform.worldToLocalMatrix.MultiplyPoint3x4(worldDest));
                        Vector3Int internalBoardDest = Vector3Int.RoundToInt(exactInternalBoardDest);
                        Vector2Int targetPos = new Vector2Int(internalBoardDest.x, internalBoardDest.z);
                        TryMoveToTargetPos(targetPos);
                    }
                }
            }
        }
    }
    void TryMoveToTargetPos(Vector2Int targetPos)
    {
        if (state.TryMovingPiece(selectedPiece, targetPos))
        {
            source.PlayOneShot(moveSound);
            PlayAITurn();
        }
    }
}
