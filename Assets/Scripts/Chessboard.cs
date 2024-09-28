using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Networking.Transport;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public enum SpecialMove {
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class Chessboard : MonoBehaviour
{
    [Header("Art stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.3f;
    [SerializeField] private float deathSpacing = 0.3f;
    [SerializeField] private float dragOffset = 1.5f;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private Transform rematchIndicator;
    [SerializeField] private Button rematchButton;
    [SerializeField] private Button fastBackwardButton;
    [SerializeField] private TextMeshProUGUI warningText;
    [SerializeField] private TextMeshProUGUI timerText;
    private Color originalColor;
    private float flashSpeed = 5f;
    [SerializeField] private GameObject promotionPopup;
    private float promotionPopupSize = 1f;
    private bool promotionPopupActivate = false;
    private bool isScalingUp = false;
    [SerializeField] private GameObject matchUI;
    [SerializeField] private GameObject replayUI;
    [SerializeField] private TextMeshProUGUI moveText;
    private bool isFastForwarding = false;
    private bool isFastReversing = false;
    private float fastTimeCounter = 0f;
    private float fastMoveDelay = 0.2f; // Adjust speed of fast forward/backward
    private ChessPiece movePiece;
    private Vector2Int startPos;
    private Vector2Int endPos;
    private bool isCapture = false;
    private bool isCheck = false;
    ChessPiece targetKing = null;
    [SerializeField] private Button saveMatchButton;
    [SerializeField] private GameObject savedMatchesUI;
    [SerializeField] private TMP_InputField saveNameInputField;


    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    // LOGIC
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private const float TURN_TIMER_MAX = 180.0f;
    private float currentTime = 0.0f;
    private float prePromotionTime = 0.0f;
    private bool gameStarted = false;
    private bool onReplay = false;
    private int currentReplayMove = 0;
    private int currentReplayChosenPieceIndex = -1;
    private bool onMenu = true;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private bool isWhiteTurn;
    private bool pausedForPromotion = false;
    private bool goingForward = true;
    private ChessPiece currentlyPromotingPawn;
    private Vector2Int promotingPosition;
    private int previousCount;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private List<ChessPieceType> chosenPieces = new List<ChessPieceType>();
    private List<Vector2Int> chosenPiecesPromotionPositions = new List<Vector2Int>();
    private List<int> promotionMoveIndexList = new List<int>();
    private int winningTeam = 2;

    // Multi logic
    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;
    private bool[] playerRematch = new bool[2];
    private int startingTeam = -1;

    private void Start() {
        fastBackwardButton.transform.localScale = new Vector3(-1, 1, 1);
        originalColor = timerText.color;

        isWhiteTurn = true;

        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();

        RegisterEvents();
    }

    private void Update() {
        if (!currentCamera) {
            currentCamera = Camera.main;
            return;
        }

        if (!onReplay) {
            if (gameStarted) {
                currentTime -= Time.deltaTime;
                timerText.text = (isWhiteTurn ? "WHITE" : "BLACK") + " TURN TIME LEFT: " + ((int)(currentTime / 60)).ToString("D2") + ":" + ((int)(currentTime % 60)).ToString("D2");
                
                if (currentTime < 10) {
                    // Fade in/out effect based on time (uses Mathf.Sin for smooth oscillation)
                    float alpha = Mathf.Abs(Mathf.Sin(Time.time * flashSpeed));
                    timerText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                } else {
                    // Reset the color to original when there’s no need for fading
                    timerText.color = originalColor;
                }

                if (currentTime < 0) {
                    // Turn time ran out, end of the game
                    CheckMate((isWhiteTurn) ? 1 : 0);
                    warningText.text = "TIME RAN OUT!";
                    pausedForPromotion = false;
                    if (localGame)
                        currentTeam = 0;
                }

                if (promotionPopupActivate) {
                    Vector3 targetScale;
                    int targetSpeed;

                    // Determine if scaling up or down
                    if (isScalingUp) {
                        targetScale = new Vector3(1.4f, 1.4f, 1.4f) * promotionPopupSize;
                        targetSpeed = 14;
                    } else {
                        targetScale = new Vector3(0.9f, 0.9f, 0.9f) * promotionPopupSize;
                        targetSpeed = 10;
                    }

                    // Apply smooth scaling using Lerp
                        promotionPopup.transform.localScale = Vector3.Lerp(promotionPopup.transform.localScale, targetScale, Time.deltaTime * targetSpeed);

                    // Check if the scaling has reached the target scale
                    if (Vector3.Distance(promotionPopup.transform.localScale, targetScale) < 0.01f) {
                        promotionPopup.transform.localScale = targetScale; // Snap to the target to avoid jitter
                        if (isScalingUp) {
                            isScalingUp = false; // Switch to scaling down
                        } else {
                            promotionPopupActivate = false; // End the scaling process
                        }
                    }
                }

                if (!pausedForPromotion) {
                    RaycastHit info;
                    Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight", "CaptureHighlight", "SpecialHighlight", "KingCheck"))) {
                        // Get the indexes of the tile I've hit
                        Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

                        // If we're hovering a tile after not hovering a tile
                        if (currentHover == -Vector2Int.one) {
                            currentHover = hitPosition;
                            tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
                        }

                        // If we were already hovering a tile, change the previous one
                        if (currentHover != hitPosition) {
                            if (chessPieces[currentHover.x, currentHover.y] != null)
                                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("CaptureHighlight") : LayerMask.NameToLayer("Tile");
                            else
                                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");

                            //if (specialMove == SpecialMove.Promotion)
                            //    tiles[availableMoves[0].x, availableMoves[0].y].layer = LayerMask.NameToLayer("SpecialHighlight");

                            if (previousCount != availableMoves.Count) {
                                for (int i = previousCount; i < availableMoves.Count; i++)
                                    tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("SpecialHighlight");
                            }

                            if(isCheck) tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("KingCheck");

                            currentHover = hitPosition;
                            tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");

                        }

                        // If we press down on the mouse
                        if (Input.GetMouseButtonDown(0)) {
                            if (chessPieces[hitPosition.x, hitPosition.y] != null) {
                                // Is it our turn?
                                if ((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && currentTeam == 0) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && currentTeam == 1)) {
                                    currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                                    // Get a list of where I can go, highlight tiles as well
                                    availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                                    previousCount = availableMoves.Count;
                                    // Get a list of special moves as well
                                    specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

                                    PreventCheck();
                                    HighlightTiles();
                                }
                            }
                        }

                        // if we are releasing the mouse button
                        if (currentlyDragging != null && Input.GetMouseButtonUp(0)) {
                            Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                            if (ContainsValidMove(ref availableMoves, new Vector2Int(hitPosition.x, hitPosition.y))) {
                                MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                                // Net implementation
                                NetMakeMove mm = new NetMakeMove();
                                mm.originalX = previousPosition.x;
                                mm.originalY = previousPosition.y;
                                mm.destinationX = hitPosition.x;
                                mm.destinationY = hitPosition.y;
                                mm.teamId = currentTeam;
                                Client.Instance.SendToServer(mm);
                            } else {
                                currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                                currentlyDragging = null;
                                RemoveHighlightTiles();
                            }
                        }

                    } else {
                        if (currentHover != -Vector2Int.one) {
                            if (chessPieces[currentHover.x, currentHover.y] != null)
                                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("CaptureHighlight") : LayerMask.NameToLayer("Tile");
                            else if (specialMove != SpecialMove.None) {
                                if (tiles[currentHover.x, currentHover.y].layer != LayerMask.NameToLayer("Tile"))
                                    tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("SpecialHighlight") : LayerMask.NameToLayer("Tile");
                            } else
                                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");


                            currentHover = -Vector2Int.one;
                            if (isCheck) tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("KingCheck");
                        }

                        if (currentlyDragging && Input.GetMouseButtonUp(0)) {
                            currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                            currentlyDragging = null;
                            RemoveHighlightTiles();
                        }
                    }

                    // If we're dragging a piece
                    if (currentlyDragging) {
                        Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
                        float distance = 0.0f;
                        if (horizontalPlane.Raycast(ray, out distance)) {
                            currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
                        }
                    }
                }
            }
        } else {

            // Check for key press to pause fastforward/fastreverse
            if (Input.GetKeyDown(KeyCode.P)) {
                isFastForwarding = false;
                isFastReversing = false;
                moveText.text = moveText.text.Replace("(Press P to Pause)\n", "Paused\n").Trim();
                replayUI.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = true;
                replayUI.transform.GetChild(1).gameObject.GetComponent<Button>().interactable = true;
                replayUI.transform.GetChild(3).gameObject.GetComponent<Button>().interactable = true;
                replayUI.transform.GetChild(4).gameObject.GetComponent<Button>().interactable = true;
            }
            // Check if fast forward or reverse is active
            if (isFastForwarding) {
                replayUI.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
                replayUI.transform.GetChild(1).gameObject.GetComponent<Button>().interactable = false;
                fastTimeCounter += Time.deltaTime;
                if (fastTimeCounter >= fastMoveDelay) {
                    if (currentReplayMove < moveList.Count) // Make sure we don't go past the last move
                    {
                        OnForwardButton(); // Forward method for one step
                        fastTimeCounter = 0f; // Reset counter
                    } else {
                        isFastForwarding = false; // Stop when end of the list is reached
                        replayUI.transform.GetChild(4).gameObject.GetComponent<Button>().interactable = false;
                        replayUI.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = true;
                        moveText.text = moveText.text.Replace("(Press P to Pause)\n", "").Trim();
                        OnForwardButton(); // additional call for checkmate if not normally happened
                    }
                }
            } else if (isFastReversing) {
                replayUI.transform.GetChild(3).gameObject.GetComponent<Button>().interactable = false;
                replayUI.transform.GetChild(4).gameObject.GetComponent<Button>().interactable = false;
                fastTimeCounter += Time.deltaTime;
                if (fastTimeCounter >= fastMoveDelay) {
                    if (currentReplayMove > 0) // Make sure we don't go before the first move
                    {
                        OnBackwardButton(); // Backward for one step
                        fastTimeCounter = 0f; // Reset counter
                    } else {
                        isFastReversing = false; // Stop when the start of the list is reached
                        replayUI.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
                        replayUI.transform.GetChild(3).gameObject.GetComponent<Button>().interactable = true;
                        replayUI.transform.GetChild(4).gameObject.GetComponent<Button>().interactable = true;
                        moveText.text = moveText.text.Replace("(Press P to Pause)\n", "").Trim();
                    }
                }
            }
        }
    }

    // Generate the board
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY) {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];

        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
    }

    private GameObject GenerateSingleTile(float tileSize, int x, int y) {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2};

        mesh.vertices = vertices;
        mesh.triangles = tris;

        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    // Spawning of the pieces
    private void SpawnAllPieces() {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0, blackTeam = 1;

        // White team
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        for(int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);

        // Black team
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
    }

    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team) {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }

    // Positioning
    private void PositionAllPieces() {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    PositionSinglePiece(x, y, true);
    }

    private void PositionSinglePiece(int x, int y, bool force = false) {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }

    private Vector3 GetTileCenter(int x, int y) {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2,0, tileSize / 2);
    }

    // Highlight Tiles
    private void HighlightTiles() {
        for (int i = 0; i < availableMoves.Count; i++)
            if (chessPieces[availableMoves[i].x, availableMoves[i].y] != null)
                tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("CaptureHighlight");
            else
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");

        if (specialMove == SpecialMove.Promotion)
            try {
                if (tiles[availableMoves[0].x, availableMoves[0].y].layer != LayerMask.NameToLayer("CaptureHighlight"))
                    tiles[availableMoves[0].x, availableMoves[0].y].layer = LayerMask.NameToLayer("SpecialHighlight");
            } catch (ArgumentOutOfRangeException) {

            }

        if (previousCount != availableMoves.Count) {
            for (int i = previousCount; i < availableMoves.Count; i++) {
                if (tiles[availableMoves[i].x, availableMoves[i].y].layer != LayerMask.NameToLayer("CaptureHighlight"))
                    tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("SpecialHighlight");
            }
        }

        if(isCheck) tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("KingCheck");
    }

    private void RemoveHighlightTiles() {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");

        availableMoves.Clear();
    }

    // Checkmate
    private void CheckMate(int team) {
        gameStarted = false;
        if(winningTeam != 3) // Invalid action prevention
            winningTeam = team;
        replayUI.gameObject.SetActive(false);
        matchUI.transform.GetChild(0).gameObject.SetActive(false);
        DisplayVictory(team);
    }

    private void DisplayVictory(int winningTeam) {
        victoryScreen.SetActive(true);
        if (winningTeam != 3)
            victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
        victoryScreen.transform.GetChild(5).GetComponent<Button>().interactable = true;
    }

    public void OnRematchButton() {
        if (localGame) {
            onReplay = false;
            warningText.gameObject.SetActive(true);
            timerText.gameObject.SetActive(true);
            matchUI.transform.GetChild(0).gameObject.SetActive(true);
            matchUI.gameObject.SetActive(true);
            replayUI.gameObject.SetActive(false);
            replayUI.transform.GetChild(0).gameObject.SetActive(false);
            replayUI.transform.GetChild(1).gameObject.SetActive(false);
            moveText.gameObject.SetActive(false);
            replayUI.transform.GetChild(3).gameObject.SetActive(false);
            replayUI.transform.GetChild(4).gameObject.SetActive(false);

            NetRematch wrm = new NetRematch();
            wrm.teamId = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            NetRematch brm = new NetRematch();
            brm.teamId = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);

            saveMatchButton.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>().text = "SAVE MATCH";
            saveMatchButton.interactable = true;

        } else {
            NetRematch rm = new NetRematch();
            rm.teamId = currentTeam;
            rm.wantRematch = 1;
            Client.Instance.SendToServer(rm);
        }
    }

    public void GameReset() {
        // UI
        currentTime = 0.0f;
        prePromotionTime = 0.0f;
        gameStarted = false;
        goingForward = true;
        if (!onReplay) {
            winningTeam = 2;
            if (localGame) {
                startingTeam = 0;
                GameUI.Instance.ChangeCamera(CameraAngle.whiteTeam);
            }
            saveMatchButton.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>().text = "SAVE MATCH";
            saveMatchButton.interactable = true;
            matchUI.transform.GetChild(0).gameObject.SetActive(true);
        }
        pausedForPromotion = false;
        matchUI.gameObject.SetActive(false);

        rematchButton.interactable = true;

        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);

        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(2).gameObject.SetActive(false);
        victoryScreen.SetActive(false);


        // Fields reset
        currentlyDragging = null;
        availableMoves.Clear();
        if(!onReplay)
            moveList.Clear();
        playerRematch[0] = playerRematch[1] = false;
        previousCount = 0;
        if (localGame)
            currentTeam = 0;

        // Clean up

        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                tiles[x, y].layer = LayerMask.NameToLayer("Tile");
                if (chessPieces[x, y] != null)
                    Destroy(chessPieces[x, y].gameObject);

                chessPieces[x, y] = null;
            }

        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);

        deadWhites.Clear();
        deadBlacks.Clear();
        if (!onReplay) {
            promotionMoveIndexList.Clear();
            chosenPiecesPromotionPositions.Clear();
            chosenPieces.Clear();
        }

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;
        if (!onMenu) {
            currentReplayChosenPieceIndex = -1;
            currentReplayMove = 0;
            GameStart(false);
        }
    }

    public void OnMenuButton() {
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);

        onMenu = true;
        onReplay = false;
        GameReset();
        matchUI.gameObject.SetActive(false);
        warningText.gameObject.SetActive(true);
        timerText.gameObject.SetActive(true);
        matchUI.transform.GetChild(0).gameObject.SetActive(true);
        replayUI.gameObject.SetActive(false);
        replayUI.transform.GetChild(0).gameObject.SetActive(false);
        replayUI.transform.GetChild(1).gameObject.SetActive(false);
        moveText.gameObject.SetActive(false);
        replayUI.transform.GetChild(3).gameObject.SetActive(false);
        replayUI.transform.GetChild(4).gameObject.SetActive(false);
        GameUI.Instance.OnLeaveFromGameMenu();

        Invoke("ShutDownRelay", 1.0f);

        // Reset some values
        playerCount = -1;
        currentTeam = -1;
    }

    public void OnSurrenderButton() {
        NetSurrender sr = new NetSurrender();
        sr.winningTeam = (currentTeam == 0) ? 1 : 0;
        sr.teamId = currentTeam;
        Client.Instance.SendToServer(sr);
        CheckMate(sr.winningTeam);
        if(pausedForPromotion) promotionPopup.gameObject.SetActive(false);
    }

    // Replay
    public void OnSavedMatchesButton() {
        savedMatchesUI.gameObject.SetActive(true);
        ReplayManager.Instance.LoadReplays();
    }

    public void OnSavedMatchesBackButton() {
        savedMatchesUI.gameObject.SetActive(false);
    }

    public void OnReplayButton() {
        if (!localGame) {
            NetRematch rm = new NetRematch();
            rm.teamId = currentTeam;
            rm.wantRematch = 0;
            Client.Instance.SendToServer(rm);

            Invoke("ShutDownRelay", 1.0f);

            localGame = true;
        }

        warningText.gameObject.SetActive(false);
        timerText.gameObject.SetActive(false);
        matchUI.transform.GetChild(0).gameObject.SetActive(false);
        matchUI.gameObject.SetActive(false);
        replayUI.gameObject.SetActive(true);
        replayUI.transform.GetChild(0).gameObject.SetActive(true);
        replayUI.transform.GetChild(1).gameObject.SetActive(true);
        replayUI.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
        replayUI.transform.GetChild(1).gameObject.GetComponent<Button>().interactable = false;
        moveText.gameObject.SetActive(true);
        moveText.text = "";
        replayUI.transform.GetChild(3).gameObject.SetActive(true);
        replayUI.transform.GetChild(4).gameObject.SetActive(true);
        replayUI.transform.GetChild(3).gameObject.GetComponent<Button>().interactable = true;
        replayUI.transform.GetChild(4).gameObject.GetComponent<Button>().interactable = true;
        onReplay = true;
        GameReset();
    }

    public void OnSaveMatchButton() {
        saveMatchButton.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>().text = "SAVED";
        saveMatchButton.interactable = false;

        List<MoveData> convertedMoveList = new List<MoveData>();
        foreach (var move in moveList) {
            MoveData moveData = new MoveData(move[0], move[1]);
            convertedMoveList.Add(moveData);
        }

        ReplayData matchReplayData = new ReplayData {
            winningTeam = winningTeam,
            startingTeam = startingTeam,
            moveList = convertedMoveList,
            chosenPieces = new List<ChessPieceType>(chosenPieces),
            chosenPiecesPromotionPositions = new List<Vector2Int>(chosenPiecesPromotionPositions),
            promotionMoveIndexList = new List<int>(promotionMoveIndexList),
            saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            saveName = saveNameInputField.text
        };
        ReplayManager.Instance.SaveReplay(matchReplayData);
        saveNameInputField.text = "";
    }

    public void OnBackToSaveMatchesButton() {
        OnMenuButton();
        OnSavedMatchesButton();
    }

    public void OnFastForwardButton() {
        isFastForwarding = true;
    }

    public void OnForwardButton() {
        if (currentReplayMove < moveList.Count) {
            goingForward = true;
            if (!isFastForwarding) {
                replayUI.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = true;
                replayUI.transform.GetChild(1).gameObject.GetComponent<Button>().interactable = true;
                replayUI.transform.GetChild(4).gameObject.GetComponent<Button>().interactable = true;
            }
            movePiece = chessPieces[moveList[currentReplayMove][0].x, moveList[currentReplayMove][0].y];
            startPos = new Vector2Int(moveList[currentReplayMove][0].x, moveList[currentReplayMove][0].y);
            endPos = new Vector2Int(moveList[currentReplayMove][1].x, moveList[currentReplayMove][1].y);
            string moveNotation = "";
            ChessPiece cp = chessPieces[moveList[currentReplayMove][0].x, moveList[currentReplayMove][0].y];
            availableMoves = cp.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            if (cp.type == ChessPieceType.Pawn || cp.type == ChessPieceType.King)
                specialMove = cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves, onReplay, currentReplayMove, goingForward);
            else
                specialMove = cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

            MoveTo(moveList[currentReplayMove][0].x, moveList[currentReplayMove][0].y, moveList[currentReplayMove][1].x, moveList[currentReplayMove][1].y);

            if (specialMove == SpecialMove.Castling) {
                moveNotation = Utilities.Instance.CastlingNotation(endPos);
            } else if (specialMove == SpecialMove.Promotion) {
                try {
                    moveNotation = Utilities.Instance.GetMoveNotation(movePiece, startPos, endPos, isCapture, isCheck, chosenPieces[currentReplayChosenPieceIndex]);
                } catch (ArgumentOutOfRangeException) {
                    moveNotation = "Surrendered during promotion!";
                }
            } else if (specialMove == SpecialMove.EnPassant) {
                moveNotation = Utilities.Instance.GetMoveNotation(movePiece, startPos, endPos, isCapture, isCheck) + "e.p.";
            } else moveNotation = Utilities.Instance.GetMoveNotation(movePiece, startPos, endPos, isCapture, isCheck);

            if (isFastForwarding)
                moveText.text = "(Press P to Pause)\n" + moveNotation;
            else moveText.text = moveNotation;

            currentReplayMove++;
        } else {
            replayUI.transform.GetChild(3).gameObject.GetComponent<Button>().interactable = false;
            replayUI.transform.GetChild(4).gameObject.GetComponent<Button>().interactable = false;
            CheckMate(winningTeam);
        }
    }

    public void OnFastBackwardButton() {
        isFastReversing = true;
    }

    public void OnBackwardButton() {
        if (currentReplayMove > 0) {
            goingForward = false;
            if (!isFastReversing) {
                replayUI.transform.GetChild(3).gameObject.GetComponent<Button>().interactable = true;
                replayUI.transform.GetChild(4).gameObject.GetComponent<Button>().interactable = true;
                replayUI.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = true;
            }
                string moveNotation = "";
            try {
                movePiece = chessPieces[moveList[currentReplayMove - 1][1].x, moveList[currentReplayMove - 1][1].y];
                startPos = new Vector2Int(moveList[currentReplayMove - 1][0].x, moveList[currentReplayMove - 1][0].y);
                endPos = new Vector2Int(moveList[currentReplayMove - 1][1].x, moveList[currentReplayMove - 1][1].y);
                ChessPiece cp = chessPieces[moveList[currentReplayMove - 1][1].x, moveList[currentReplayMove - 1][1].y];
                availableMoves = cp.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                if (cp.type == ChessPieceType.Pawn || cp.type == ChessPieceType.King) {
                    if (cp.type == ChessPieceType.King || (deadWhites.Count == 0 && deadBlacks.Count == 0))
                        specialMove = cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves, onReplay, currentReplayMove, goingForward);
                    else {
                        ChessPiece lastDeadPiece;
                        if ((deadBlacks.Count == 0 && !isWhiteTurn) || (deadWhites.Count == 0 && isWhiteTurn)) // No dead pieces = no previous capture
                            lastDeadPiece = null;
                        else
                            lastDeadPiece = (cp.team == 0) ? deadBlacks[deadBlacks.Count - 1] : deadWhites[deadWhites.Count - 1];

                        specialMove = cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves, onReplay, currentReplayMove, goingForward, lastDeadPiece);
                    }

                } else {
                    specialMove = cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
                }
            } catch (NullReferenceException) {
                moveText.text = "Spam or invalid move detected, closing replay...";
                CheckMate(3);
                return;
            }

            MoveTo(moveList[currentReplayMove - 1][1].x, moveList[currentReplayMove - 1][1].y, moveList[currentReplayMove - 1][0].x, moveList[currentReplayMove - 1][0].y);

            if(specialMove == SpecialMove.Promotion)
                movePiece = chessPieces[moveList[currentReplayMove - 1][0].x, moveList[currentReplayMove - 1][0].y];
            moveNotation = Utilities.Instance.GetMoveNotation(movePiece, startPos, endPos, isCapture, isCheck, ChessPieceType.None, goingForward);

            if (isFastReversing)
                moveText.text = "(Press P to Pause)\n" + moveNotation;
            else moveText.text = moveNotation;

            currentReplayMove--;
        } else {
            replayUI.transform.GetChild(0).gameObject.GetComponent<Button>().interactable = false;
            replayUI.transform.GetChild(1).gameObject.GetComponent<Button>().interactable = false;
        }
    }


    // Special Moves
    private void ProcessSpecialMove() {
        if(specialMove == SpecialMove.EnPassant) {
            Vector2Int[] newMove = null;
            ChessPiece myPawn = null;
            Vector2Int[] targetPawnPosition = null;
            ChessPiece enemyPawn = null;
            if (!onReplay) {
                newMove = moveList[moveList.Count - 1];
                myPawn = chessPieces[newMove[1].x, newMove[1].y];
                targetPawnPosition = moveList[moveList.Count - 2];
                enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];
            } else if (goingForward) {
                newMove = moveList[currentReplayMove];
                myPawn = chessPieces[newMove[1].x, newMove[1].y];
                targetPawnPosition = moveList[currentReplayMove - 1];
                enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];
            } else {
                targetPawnPosition = moveList[currentReplayMove - 1];
                myPawn = chessPieces[targetPawnPosition[0].x, targetPawnPosition[0].y];
                enemyPawn = (myPawn.team == 0) ? deadBlacks[deadBlacks.Count - 1] : deadWhites[deadWhites.Count - 1];

                chessPieces[enemyPawn.currentX, enemyPawn.currentY] = enemyPawn;
                PositionSinglePiece(enemyPawn.currentX, enemyPawn.currentY);

                enemyPawn.SetScale(Vector3.one);
                enemyPawn.capturedPosition = -Vector2Int.one;  // Reset the capture position

                _ = (enemyPawn.team == 0) ? deadWhites.Remove(enemyPawn) : deadBlacks.Remove(enemyPawn); // Remove the piece from graveyard

                return;
            }

            if (myPawn.currentX == enemyPawn.currentX) {
                if(myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1) {
                    enemyPawn.capturedPosition = new Vector2Int(enemyPawn.currentX, enemyPawn.currentY);  // Store the capture position
                    // Store the capture move
                    isCapture = true;
                    if (onReplay)
                        enemyPawn.capturedMove = currentReplayMove;

                    if (enemyPawn.team == 0) {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(
                            new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + (Vector3.forward * deathSpacing) * deadWhites.Count);
                    } else {
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(
                            new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + (Vector3.back * deathSpacing) * deadBlacks.Count);
                    }
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                } else isCapture = false;
            } else isCapture = false;
        }

        if(specialMove == SpecialMove.Promotion) {
            Vector2Int[] lastMove = null;
            ChessPiece targetPawn = null;
            if (!onReplay)
                lastMove = moveList[moveList.Count - 1];
            else if (!goingForward) {
                currentReplayChosenPieceIndex--;
                return;
            } else lastMove = moveList[currentReplayMove];

            targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if (targetPawn.type == ChessPieceType.Pawn) {
                currentlyPromotingPawn = targetPawn;
                promotingPosition = lastMove[1];

                if (!onReplay) {
                    pausedForPromotion = true;

                    isWhiteTurn = !isWhiteTurn;
                    currentTime = prePromotionTime;
                    if (localGame)
                        currentTeam = (currentTeam == 0) ? 1 : 0;

                    if (targetPawn.team == currentTeam) {
                        promotionPopup.gameObject.SetActive(true);
                        promotionPopupActivate = true;
                        isScalingUp = true;
                        promotionPopup.transform.localScale = Vector3.zero;
                        Vector3 worldPosition = targetPawn.transform.position;
                        Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
                        screenPosition.y += 100;
                        promotionPopup.transform.position = screenPosition;
                    }
                } else {
                    currentReplayChosenPieceIndex++;
                    try {
                        OnPromotionSelected(chosenPieces[currentReplayChosenPieceIndex]);
                    } catch (ArgumentOutOfRangeException) {
                        moveText.text = "Surrendered during promotion!";
                    }
                }
            }
        }

        if(specialMove == SpecialMove.Castling) {
            Vector2Int[] lastMove = null;
            if (!onReplay)
                lastMove = moveList[moveList.Count - 1];
            else if(goingForward)
                lastMove = moveList[currentReplayMove];
            else lastMove = moveList[currentReplayMove - 1];

            if (!goingForward) {
                // Left Rook
                if (lastMove[1].x == 2) {
                    if (lastMove[1].y == 0) { // White side
                        ChessPiece rook = chessPieces[3, 0];
                        chessPieces[0, 0] = rook;
                        PositionSinglePiece(0, 0);
                        chessPieces[3, 0] = null;
                    } else if (lastMove[1].y == 7) { // Black side
                        ChessPiece rook = chessPieces[3, 7];
                        chessPieces[0, 7] = rook;
                        PositionSinglePiece(0, 7);
                        chessPieces[3, 7] = null;
                    }
                }
                // Right Rook
                else if (lastMove[1].x == 6) {
                    if (lastMove[1].y == 0) { // White side
                        ChessPiece rook = chessPieces[5, 0];
                        chessPieces[7, 0] = rook;
                        PositionSinglePiece(7, 0);
                        chessPieces[5, 0] = null;
                    } else if (lastMove[1].y == 7) { // Black side
                        ChessPiece rook = chessPieces[5, 7];
                        chessPieces[7, 7] = rook;
                        PositionSinglePiece(7, 7);
                        chessPieces[5, 7] = null;
                    }
                }
            } else {
                // Left Rook
                if (lastMove[1].x == 2) {
                    if(lastMove[1].y == 0) { // White side
                        ChessPiece rook = chessPieces[0, 0];
                        chessPieces[3, 0] = rook;
                        PositionSinglePiece(3, 0);
                        chessPieces[0, 0] = null;
                    } else if (lastMove[1].y == 7) { // Black side
                        ChessPiece rook = chessPieces[0, 7];
                        chessPieces[3, 7] = rook;
                        PositionSinglePiece(3, 7);
                        chessPieces[0, 7] = null;
                    }
                }
                // Right Rook
                else if (lastMove[1].x == 6) {
                    if (lastMove[1].y == 0) { // White side
                        ChessPiece rook = chessPieces[7, 0];
                        chessPieces[5, 0] = rook;
                        PositionSinglePiece(5, 0);
                        chessPieces[7, 0] = null;
                    } else if (lastMove[1].y == 7) { // Black side
                        ChessPiece rook = chessPieces[7, 7];
                        chessPieces[5, 7] = rook;
                        PositionSinglePiece(5, 7);
                        chessPieces[7, 7] = null;
                    }
                }
            }
        }
    }

    private void PreventCheck() {
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    if (chessPieces[x, y].type == ChessPieceType.King)
                        if (chessPieces[x, y].team == currentlyDragging.team)
                        targetKing = chessPieces[x, y];

        // Since we're sending ref availableMoves, we will be deleting moves that are putting us in check
        SimulateMoveForSinglePiece(currentlyDragging, ref availableMoves, targetKing);
    }

    private void SimulateMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing) {
        // Save the current values to reset after the function call
        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        // Going through all the moves and check if we're in check
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSim = new Vector2Int(targetKing.currentX, targetKing.currentY);
            // Did we simulate the King's move
            if (cp.type == ChessPieceType.King)
                kingPositionThisSim = new Vector2Int(simX, simY);

            // Copy the [,] and not a reference
            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if (chessPieces[x, y] != null) {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                            simAttackingPieces.Add(simulation[x, y]);
                    }
                }
            }

            // Simulate that move
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            // Did one of the pieces got taken down during our simulation
            ChessPiece deadPiece = simAttackingPieces.Find(c => c.currentX == simX && c.currentY == simY);
            if (deadPiece != null)
                simAttackingPieces.Remove(deadPiece);

            // Get all the simulated attacking pieces moves
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for (int a = 0; a < simAttackingPieces.Count; a++)
            {
                List<Vector2Int> pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation, TILE_COUNT_X, TILE_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                    simMoves.Add(pieceMoves[b]);
            }

            // Is the King in trouble? If so, remove the move
            if(ContainsValidMove(ref simMoves, kingPositionThisSim)) {
                movesToRemove.Add(moves[i]);
            }

            // Reset the actual CP data
            cp.currentX = actualX;
            cp.currentY = actualY;
        }

        // Remove from the current available move list
        for (int i = 0; i < movesToRemove.Count; i++)
            moves.Remove(movesToRemove[i]);
    }

    private int CheckForCheckmate() {
        Vector2Int[] lastMove = moveList[moveList.Count - 1];
        if (onReplay)
            lastMove = moveList[currentReplayMove];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        if(targetKing != null) {
            if(targetKing.currentX != lastMove[0].x && targetKing.currentY != lastMove[0].y)
                tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("Tile");
            else tiles[lastMove[0].x, lastMove[0].y].layer = LayerMask.NameToLayer("Tile");
            targetKing = null;
        }

        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null) {
                    if (chessPieces[x, y].team == targetTeam) {
                        defendingPieces.Add(chessPieces[x, y]);
                        if (chessPieces[x, y].type == ChessPieceType.King)
                            targetKing = chessPieces[x, y];
                    } else {
                        attackingPieces.Add(chessPieces[x, y]);
                    }
                }

        // Is the King attacked right now?
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            List<Vector2Int> pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
                currentAvailableMoves.Add(pieceMoves[b]);
        }

        // Are we in check right now?
        if(ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY))) {
            // King is under attack, can we move something to help him?
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                // Since we're sending ref defendingMoves, we will be deleting moves that are putting us in check
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0) {
                    warningText.text = "CHECK!";
                    tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("KingCheck");
                    if(goingForward)
                        isCheck = true;
                    return 0;
                }
            }

            tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("Tile");
            warningText.text = "CHECKMATE!";
            isCheck = false;
            return 1; // Checkmate exit
        } else {
            isCheck = false;
            if (targetKing.currentX != lastMove[0].x && targetKing.currentY != lastMove[0].y)
                tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("Tile");
            else tiles[lastMove[0].x, lastMove[0].y].layer = LayerMask.NameToLayer("Tile");
            for (int i = 0; i < defendingPieces.Count; i++) {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                // Since we're sending ref defendingMoves, we will be deleting moves that are putting us in check
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0) {
                    warningText.text = "";
                    return 0;
                }
            }

            warningText.text = "STALEMATE!";
            return 2; // Stalemate exit
        }
    }

    // Operations
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos) {
        for (int i = 0; i < moves.Count; i++)
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;

        return false;
    }
    private void MoveTo(int originalX, int originalY, int x, int y) {
        ChessPiece cp = chessPieces[originalX, originalY];
        Vector2Int previousPosition = new Vector2Int(originalX, originalY);

        if (goingForward) {
            // Is there another piece on the target position?
            if (chessPieces[x, y] != null) {
                ChessPiece ocp = chessPieces[x, y];

                if (cp.team == ocp.team)
                    return;

                isCapture = true;
                // If its the enemy team
                ocp.capturedPosition = new Vector2Int(x, y);  // Store the capture position
                // Store the capture move
                if(onReplay)
                    ocp.capturedMove = currentReplayMove;

                if (ocp.team == 0) {
                    if (ocp.type == ChessPieceType.King)
                        CheckMate(1);

                    deadWhites.Add(ocp);
                    ocp.SetScale(Vector3.one * deathSize);
                    ocp.SetPosition(
                        new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                        - bounds
                        + new Vector3(tileSize / 2, 0, tileSize / 2)
                        + (Vector3.forward * deathSpacing) * deadWhites.Count);
                } else {
                    if (ocp.type == ChessPieceType.King)
                        CheckMate(0);

                    deadBlacks.Add(ocp);
                    ocp.SetScale(Vector3.one * deathSize);
                    ocp.SetPosition(
                        new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                        - bounds
                        + new Vector3(tileSize / 2, 0, tileSize / 2)
                        + (Vector3.back * deathSpacing) * deadBlacks.Count);
                }
            } else isCapture = false;
            chessPieces[previousPosition.x, previousPosition.y] = null;
        } else {
            isCheck = false;
            isCapture = false;
            // Was there another piece on the target position?
            Vector2Int previousCapturePosition;
            if ((deadWhites.Count == 0 && isWhiteTurn) || (deadBlacks.Count == 0 && !isWhiteTurn)) // No dead pieces = no previous capture
                previousCapturePosition = -Vector2Int.one;
            else
                previousCapturePosition = (!isWhiteTurn) ? deadBlacks[deadBlacks.Count - 1].capturedPosition : deadWhites[deadWhites.Count - 1].capturedPosition;

            if (chessPieces[x, y] == null && previousCapturePosition.x == originalX && previousCapturePosition.y == originalY) { // It was a capture move, restore from graveyard
                ChessPiece ocp = (!isWhiteTurn) ? deadBlacks[deadBlacks.Count - 1] : deadWhites[deadWhites.Count - 1];
                if (ocp.capturedMove == currentReplayMove - 1) { // If its the right order
                    chessPieces[originalX, originalY] = ocp;
                    PositionSinglePiece(originalX, originalY);

                    ocp.SetScale(Vector3.one);
                    ocp.capturedPosition = -Vector2Int.one;  // Reset the capture position

                    _ = (ocp.team == 0) ? deadWhites.Remove(ocp) : deadBlacks.Remove(ocp); // Remove the piece from graveyard
                }
            } else chessPieces[previousPosition.x, previousPosition.y] = null;
        }

        chessPieces[x, y] = cp;
        PositionSinglePiece(x, y);
        if (onReplay) {
            if (!goingForward && cp.promoted) {
                int index = chosenPiecesPromotionPositions.IndexOf(previousPosition);
                if (index != -1 && promotionMoveIndexList[currentReplayChosenPieceIndex] >= currentReplayMove) {
                    currentlyPromotingPawn = cp;
                    promotingPosition = new Vector2Int(x, y);
                    OnPromotionSelected(ChessPieceType.Pawn);

                    ChessPieceType cpType = cp.type;
                    cp = chessPieces[x, y];

                    availableMoves = cp.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);

                    try {
                        specialMove = cp.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
                    } catch (NullReferenceException) {
                        specialMove = SpecialMove.None;
                    }

                    if (specialMove != SpecialMove.Promotion) {
                        currentlyPromotingPawn = cp;
                        promotingPosition = new Vector2Int(x, y);
                        OnPromotionSelected(cpType);
                    }
                }
            }
        }

        if (!pausedForPromotion) {
            isWhiteTurn = !isWhiteTurn;
            prePromotionTime = currentTime;
            currentTime = TURN_TIMER_MAX;
            if (localGame)
                currentTeam = (currentTeam == 0) ? 1 : 0;
            if(!onReplay)
                moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });
        }

        ProcessSpecialMove();

        if(currentlyDragging)
            currentlyDragging = null;
        RemoveHighlightTiles();

        if (goingForward) {
            switch (CheckForCheckmate()) {
                default:
                    break;
                case 1:
                    CheckMate(cp.team);
                    break;
                case 2:
                    CheckMate(2);
                    break;
            }
        }

        return;
    }
    private Vector2Int LookupTileIndex(GameObject hitInfo) {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if(tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);

        return -Vector2Int.one; // Invalid
    }

    #region
    private void RegisterEvents() {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.S_REMATCH += OnRematchServer;
        NetUtility.S_PROMOTION += OnPromotionServer;
        NetUtility.S_SURRENDER += OnSurrenderServer;

        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;
        NetUtility.C_REMATCH += OnRematchClient;
        NetUtility.C_PROMOTION += OnPromotionClient;
        NetUtility.C_SURRENDER += OnSurrenderClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;
        GameUI.Instance.PromotionSelected += OnPromotionSelected;
        ReplayManager.Instance.OnReplayLoaded += ReplayManager_OnReplayLoaded;
    }

    private void UnRegisterEvents() {
        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_REMATCH -= OnRematchServer;
        NetUtility.S_PROMOTION -= OnPromotionServer;
        NetUtility.S_SURRENDER -= OnSurrenderServer;

        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;
        NetUtility.C_REMATCH -= OnRematchClient;
        NetUtility.C_PROMOTION -= OnPromotionClient;
        NetUtility.C_SURRENDER -= OnSurrenderClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;
        GameUI.Instance.PromotionSelected -= OnPromotionSelected;
        ReplayManager.Instance.OnReplayLoaded -= ReplayManager_OnReplayLoaded;
    }

    // Server
    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn) {
        // Client has connected, assign a team and return a message back to him
        NetWelcome nw = msg as NetWelcome;

        // Assign a team
        nw.AssignedTeam = ++playerCount;

        // Return back to the client
        Server.Instance.SendToClient(cnn, nw);

        // If full, start the game
        if(playerCount == 1)
            Server.Instance.Broadcast(new NetStartGame());
    }

    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn) {
        // Receive the message and broadcast it back
        NetMakeMove mm = msg as NetMakeMove;

        // This is where you could do some validation checks!
        // --

        // Receive, and just broadcast it back
        Server.Instance.Broadcast(mm);
    }

    private void OnRematchServer(NetMessage msg, NetworkConnection cnn) {
        Server.Instance.Broadcast(msg);
    }

    private void OnPromotionServer(NetMessage msg, NetworkConnection cnn) {
        Server.Instance.Broadcast(msg);
    }

    private void OnSurrenderServer(NetMessage msg, NetworkConnection cnn) {
        Server.Instance.Broadcast(msg);
    }
    // Client
    private void OnWelcomeClient(NetMessage msg) {
        // Receive the network message
        NetWelcome nw = msg as NetWelcome;

        // Assign a team
        currentTeam = nw.AssignedTeam;
        if(startingTeam == -1)
            startingTeam = nw.AssignedTeam;

        Debug.Log($"My assigned team is {nw.AssignedTeam}");

        if(localGame && currentTeam == 0)
            Server.Instance.Broadcast(new NetStartGame());
    }

    private void OnStartGameClient(NetMessage msg) {
        GameUI.Instance.ChangeCamera((currentTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
        GameStart(true);
        if (onReplay) {
            OnReplayButton();
            GameUI.Instance.ChangeCamera((startingTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
        }
    }

    private void OnMakeMoveClient(NetMessage msg) {
        NetMakeMove mm = msg as NetMakeMove;

        Debug.Log($"MM: {mm.teamId} : {mm.originalX} {mm.originalY} -> {mm.destinationX} {mm.destinationY}");

        if(mm.teamId != currentTeam) {
            ChessPiece target = chessPieces[mm.originalX, mm.originalY];

            availableMoves = target.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            specialMove = target.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

            MoveTo(mm.originalX, mm.originalY, mm.destinationX, mm.destinationY);

        }
    }

    private void OnRematchClient(NetMessage msg) {
        // Receive the connection message
        NetRematch rm = msg as NetRematch;

        // Set the boolean for rematch
        playerRematch[rm.teamId] = rm.wantRematch == 1;

        // Activate the piece of UI
        if (rm.teamId != currentTeam && !localGame) {
            rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
            if(rm.wantRematch != 1) {
                rematchButton.interactable = false;
            }
        }

        // If both wants to rematch
        if (playerRematch[0] && playerRematch[1]) {
            onMenu = false;
            GameReset();
        }
    }

    private void OnPromotionClient(NetMessage msg) {
        // Receive the connection message
        NetPromotion pr = msg as NetPromotion;

        // Activate the piece of UI
        if (pr.teamId != currentTeam && !localGame)
            OnPromotionSelected((ChessPieceType)pr.promotionIndex);
    }

    private void OnSurrenderClient(NetMessage msg) {
        NetSurrender sr = msg as NetSurrender;

        if (sr.teamId != currentTeam) {
            CheckMate(sr.winningTeam);
            if (pausedForPromotion) promotionPopup.gameObject.SetActive(false);
        }
    }

    private void ShutDownRelay() {
        Client.Instance.Shutdown();
        Server.Instance.Shutdown();
    }

    private void OnSetLocalGame(bool v) {
        onMenu = false;
        playerCount = -1;
        currentTeam = -1;
        localGame = v;
        if (localGame) {
            rematchButton.interactable = true;
            rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
            rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);
        }
    }

    private void OnPromotionSelected(ChessPieceType chosenPiece) {
        if(currentlyPromotingPawn != null) {
            int team = currentlyPromotingPawn.team;

            ChessPiece newPiece = SpawnSinglePiece(chosenPiece, team);

            newPiece.transform.position = currentlyPromotingPawn.transform.position;

            Destroy(currentlyPromotingPawn.gameObject);

            chessPieces[promotingPosition.x, promotingPosition.y] = newPiece;
            PositionSinglePiece(promotingPosition.x, promotingPosition.y);

            if (goingForward) {
                newPiece.promoted = true;
                if (!onReplay) {
                    chosenPieces.Add(chosenPiece);
                    chosenPiecesPromotionPositions.Add(promotingPosition);
                    promotionMoveIndexList.Add(moveList.Count);
                }
            }

            promotionPopup.transform.localScale = Vector3.one * promotionPopupSize;
            promotionPopup.gameObject.SetActive(false);
            promotionPopupActivate = false;
            isScalingUp = false;
            pausedForPromotion = false;

            if (!onReplay) {
                isWhiteTurn = !isWhiteTurn;
                prePromotionTime = currentTime;
                currentTime = TURN_TIMER_MAX;
                CheckForCheckmate();

                if (localGame) {
                    currentTeam = (currentTeam == 0) ? 1 : 0;
                }
            }

            NetPromotion pr = new NetPromotion();
            pr.teamId = currentTeam;
            pr.promotionIndex = (int)chosenPiece;
            Client.Instance.SendToServer(pr);
        }
    }

    private void ReplayManager_OnReplayLoaded(object sender, ReplayData e) {
        savedMatchesUI.gameObject.SetActive(false);
        GameUI.Instance.OnLocalGameButton();
        onReplay = true;
        GameUI.Instance.ChangeCamera((e.startingTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
        List<Vector2Int[]> loadedMoveList = new List<Vector2Int[]>();

        foreach (MoveData moveData in e.moveList) {
            Vector2Int[] move = new Vector2Int[2];
            move[0] = moveData.start;
            move[1] = moveData.end;
            loadedMoveList.Add(move);
        }

        // Set the current replay data in the game
        moveList = loadedMoveList;

        chosenPieces = e.chosenPieces;
        chosenPiecesPromotionPositions = e.chosenPiecesPromotionPositions;
        promotionMoveIndexList = e.promotionMoveIndexList;

        winningTeam = e.winningTeam;
        startingTeam = e.startingTeam;

        saveMatchButton.transform.GetChild(0).gameObject.GetComponent<TextMeshProUGUI>().text = "SAVED";
        saveMatchButton.interactable = false;
    }

    private void GameStart(bool firstStart) {
        gameStarted = true;
        matchUI.gameObject.SetActive(true);
        warningText.text = "";
        saveNameInputField.text = "";
        if (firstStart)
            currentTime = TURN_TIMER_MAX + 1.5f;
        else
            currentTime = TURN_TIMER_MAX;
        timerText.text = "TIME LEFT: " + currentTime;
    }
    #endregion
}
