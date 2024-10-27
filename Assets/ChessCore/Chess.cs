using NUnit.Framework;
using System;
using System.Collections;
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
    BoardState state;
    public GameObject[] chessPiecePrefabs;
    public GameObject optionVisualizer;
    Transform piecesContainer;
    public ChessPiece[] selectedPiece = new ChessPiece[2];
    public AudioClip moveSound;
    public AudioClip selectSound;
    AudioSource source;
    public float squaresPerSecond;
    List<GameObject> optionObjects = new();
    double lastTurnTime = 0;
    int turnIndex = 0;
    //cooldown between turns before the other player can do its turn
    public double turnCooldown = 1;

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
                    GameObject pieceObject = Instantiate(chessPiecePrefabs[(int)type + ChessPieceTypeCount * playerIndex], transform);
                    int relativeRowOffset = type == ChessPieceType.Pawn ? 1 : 0;
                    var colOffset = type switch
                    {
                        ChessPieceType.Rook => i == 1 ? 7 : 0,
                        ChessPieceType.Knight => i == 1 ? 6 : 1,
                        ChessPieceType.Bishop => i == 1 ? 5 : 2,
                        ChessPieceType.Queen => 3,
                        ChessPieceType.King => 4,
                        //pawn and default
                        _ => i,
                    };
                    int absoluteRowOffset = state.players[playerIndex].TransformY(relativeRowOffset);
                    ChessPiece p = new() { gameObject = pieceObject, position = new Vector2Int(colOffset, absoluteRowOffset), type = type, lastRenderedType = type, owner = state.players[playerIndex] };
                    state.squares.Add(p.position, p);
                    state.players[playerIndex].pieces.Add(p);
                }
            }
        }
        state.players[(int)ChessPlayerColor.black].AI = true;

    }

    // Update is called once per frame
    void Update()
    {

        //converting from internal board size to external board size
        const float maxInternalOffset = 3.5f;
        const float maxExternalOffset = 5.2f;
        Matrix4x4 internalToExternal = Matrix4x4.TRS(new Vector3(-maxExternalOffset, 0, -maxExternalOffset), Quaternion.identity, Vector3.one * maxExternalOffset / maxInternalOffset);

        bool canDoTurn = Time.timeSinceLevelLoadAsDouble > lastTurnTime + turnCooldown;

        if (canDoTurn)
        {
            if (state.players[turnIndex].AI)
            {
                PlayAITurn();
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                {
                    //player can move objects
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        Transform objectHit = hit.transform;
                        bool clickUsed = false;
                        // Do something with the object that was hit by the raycast.
                        foreach (ChessPiece p in state.players[turnIndex].pieces)
                        {
                            if (p.gameObject == objectHit.gameObject)
                            {
                                selectedPiece[turnIndex] = p;
                                source.PlayOneShot(selectSound);
                                clickUsed = true;
                                break;
                            }
                        }
                        if (!clickUsed)
                        {
                            //clicked on a piece on the opposite side
                            foreach (ChessPiece p in state.players[1 - turnIndex].pieces)
                            {
                                if (p.gameObject == objectHit.gameObject)
                                {
                                    //try attacking this piece
                                    clickUsed = true;
                                    TryMoveToTargetPos(p.position);
                                    break;
                                }
                            }
                        }
                        if (objectHit == transform)
                        {
                            if (selectedPiece[turnIndex] != null)
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
        }

        //visualise current board state
        for (int i = 0; i < state.players.Length; i++)
        {
            ChessPlayer player = state.players[i];
            foreach (ChessPiece piece in player.pieces)
            {
                if (piece.type != piece.lastRenderedType)
                {
                    Destroy(piece.gameObject);
                    piece.gameObject = Instantiate(chessPiecePrefabs[(int)piece.type + ChessPieceTypeCount * piece.owner.id], transform);
                    piece.lastRenderedType = piece.type;

                }
                Vector3 destination = internalToExternal.MultiplyPoint3x4(new Vector3(piece.position.x, 0, piece.position.y));
                Vector3 difference = destination - piece.gameObject.transform.localPosition;
                piece.gameObject.transform.localPosition += difference.normalized * Mathf.Min(difference.magnitude, squaresPerSecond * Time.deltaTime);
            }
            int deadCount = 0;
            foreach (ChessPiece piece in player.deadPieces)
            {
                Vector2Int displayPosition = new Vector2Int(i == 1 ? BoardState.boardSize + 1 : -2, deadCount++);
                piece.gameObject.transform.localPosition = internalToExternal.MultiplyPoint3x4(new Vector3(displayPosition.x, 0, displayPosition.y));
            }
        }

        //visualize options

        //delete all previous option objects
        foreach (GameObject gameObject in optionObjects)
        {
            Destroy(gameObject);
        }

        if (selectedPiece[turnIndex] != null && state.Alive(selectedPiece[turnIndex]))
        {
            List<PieceMovement> options = state.GetPieceOptions(selectedPiece[turnIndex]);
            foreach (PieceMovement option in options)
            {
                //visualize by drawing rectangle
                GameObject optionObject = Instantiate(optionVisualizer, transform);
                optionObject.GetComponent<MeshRenderer>().material.color = Color.green;
                optionObjects.Add(optionObject);
                optionObject.transform.localPosition = internalToExternal.MultiplyPoint3x4(new Vector3(option.to.x, 0, option.to.y));
            }
        }
    }
    void ProcessTurn()
    {
        source.PlayOneShot(moveSound);
        turnIndex = 1 - turnIndex;
        lastTurnTime = Time.timeSinceLevelLoadAsDouble;
    }
    void TryMoveToTargetPos(Vector2Int targetPos)
    {
        if (selectedPiece[turnIndex] != null && state.TryMovingPiece(selectedPiece[turnIndex], targetPos))
        {
            ProcessTurn();
        }
    }
}
