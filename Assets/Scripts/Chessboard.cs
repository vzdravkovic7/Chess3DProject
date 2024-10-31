using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Networking.Transport;
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
    private bool isCapture = false;
    private bool isCheck = false;
    ChessPiece targetKing = null;

    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;

    // Events
    public event EventHandler OnReplayDataLoaded;

    // LOGIC
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    public const int TILE_COUNT_X = 8;
    public const int TILE_COUNT_Y = 8;
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
    public SpecialMove specialMove;
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
        SetInitialSettings();
        GenerateBoard();
        InitializePieces();
        RegisterEvents();
    }

    private void SetInitialSettings() {
        GameUIManager.Instance.SetInitialSettings();
        isWhiteTurn = true;
    }

    private void GenerateBoard() {
        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
    }

    private void InitializePieces() {
        SpawnAllPieces();
        PositionAllPieces();
    }

    private void Update() {
        if (!currentCamera) {
            currentCamera = Camera.main;
            return;
        }

        if (!onReplay) {
            if (gameStarted) {
                UpdateTimer();

                if (GameUIManager.Instance.GetPromotionPopupActivate()) {
                    GameUIManager.Instance.UpdatePromotionPopup();
                }

                if (!pausedForPromotion) {
                    HandleTileHoverAndSelection();
                }
            }
        } else {
            ReplayGameManager.Instance.HandleReplayControls();
        }
    }

    private void UpdateTimer() {
        currentTime -= Time.deltaTime;
        GameUIManager.Instance.UpdateTimer(currentTime, isWhiteTurn);

        if (currentTime < 0) {
            EndGameDueToTimeout();
        }
    }

    private void EndGameDueToTimeout() {
        CheckMate((isWhiteTurn) ? 1 : 0);
        GameUIManager.Instance.HandleGameWarning("TIME RAN OUT!");
        pausedForPromotion = false;
        if (localGame)
            currentTeam = 0;
    }

    private void HandleTileHoverAndSelection() {
        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight", "CaptureHighlight", "SpecialHighlight", "KingCheck"))) {
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            UpdateTileHover(hitPosition);

            if (Input.GetMouseButtonDown(0)) {
                SelectPiece(hitPosition);
            }

            if (currentlyDragging != null && Input.GetMouseButtonUp(0)) {
                MovePiece(hitPosition);
            }
        } else {
            ClearTileHover();

            if (currentlyDragging && Input.GetMouseButtonUp(0)) {
                ResetDraggingPiece();
            }
        }

        // If we're dragging a piece
        if (currentlyDragging) {
            DisposeDraggingPiece(ray);
        }
    }

    private void UpdateTileHover(Vector2Int hitPosition) {
        if (currentHover == -Vector2Int.one) {
            currentHover = hitPosition;
            tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
        }

        if (currentHover != hitPosition) {
            ResetTileLayer(currentHover, false);
            currentHover = hitPosition;
            tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
        }
    }

    private void SelectPiece(Vector2Int hitPosition) {
        if (chessPieces[hitPosition.x, hitPosition.y] != null) {
            bool isCorrectTurn = (chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && currentTeam == 0) ||
                                 (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && currentTeam == 1);

            if (isCorrectTurn) {
                currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                previousCount = availableMoves.Count;
                specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

                PreventCheck();
                GameUIManager.Instance.HighlightTiles(ref availableMoves, ref chessPieces, ref tiles, specialMove, previousCount, isCheck, targetKing);
            }
        }
    }

    private void MovePiece(Vector2Int hitPosition) {
        Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

        if (ContainsValidMove(ref availableMoves, hitPosition)) {
            MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

            NetMakeMove mm = new NetMakeMove {
                originalX = previousPosition.x,
                originalY = previousPosition.y,
                destinationX = hitPosition.x,
                destinationY = hitPosition.y,
                teamId = currentTeam
            };
            Client.Instance.SendToServer(mm);
        } else {
            currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
            currentlyDragging = null;
            GameUIManager.Instance.RemoveHighlightTiles(ref availableMoves, ref chessPieces, ref tiles);
        }
    }

    private void ClearTileHover() {
        if (currentHover != -Vector2Int.one) {
            ResetTileLayer(currentHover, true);
            currentHover = -Vector2Int.one;
        }
    }

    private void ResetDraggingPiece() {
        currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
        currentlyDragging = null;
        GameUIManager.Instance.RemoveHighlightTiles(ref availableMoves, ref chessPieces, ref tiles);
    }

    private void DisposeDraggingPiece(Ray ray) {
        Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
        float distance = 0.0f;
        if (horizontalPlane.Raycast(ray, out distance)) {
            currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
        }
    }

    private void ResetTileLayer(Vector2Int position, bool clearing) {
        if (chessPieces[position.x, position.y] != null) {
            tiles[position.x, position.y].layer = ContainsValidMove(ref availableMoves, position) ?
                                                   LayerMask.NameToLayer("CaptureHighlight") :
                                                   LayerMask.NameToLayer("Tile");
        } else if (clearing && specialMove != SpecialMove.None) {
            tiles[position.x, position.y].layer = ContainsValidMove(ref availableMoves, position) ?
                                                   LayerMask.NameToLayer("SpecialHighlight") :
                                                   LayerMask.NameToLayer("Tile");
        } else {
            tiles[position.x, position.y].layer = ContainsValidMove(ref availableMoves, position) ?
                                                   LayerMask.NameToLayer("Highlight") :
                                                   LayerMask.NameToLayer("Tile");
        }

        if (!clearing && previousCount != availableMoves.Count) {
            for (int i = previousCount; i < availableMoves.Count; i++)
                tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("SpecialHighlight");
        }

        if (isCheck) {
            tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("KingCheck");
        }
    }

    public bool GetGoingForward() {
        return goingForward;
    }

    public bool GetOnReplay() {
        return onReplay;
    }

    public bool GetIsWhiteTurn() {
        return isWhiteTurn;
    }

    public bool GetIsCheck() {
        return isCheck;
    }

    public bool GetIsCapture() {
        return isCapture;
    }

    public int GetWinningTeam() {
        return winningTeam;
    }

    public void SetCurrentReplayMove(int currentReplayMove) {
        this.currentReplayMove = currentReplayMove;
    }

    public int GetCurrentReplayChosenPieceIndex() {
        return currentReplayChosenPieceIndex;
    }

    public void SetGoingForward(bool goingForward) {
        this.goingForward = goingForward;
    }

    public void ProvideGameState(
    ref List<Vector2Int[]> moveList,
    ref ChessPiece[,] chessPieces,
    ref List<Vector2Int> availableMoves,
    ref List<ChessPiece> deadWhites,
    ref List<ChessPiece> deadBlacks,
    ref List<ChessPieceType> chosenPieces) {
        // Pass the references of each private field
        moveList = this.moveList;
        chessPieces = this.chessPieces;
        availableMoves = this.availableMoves;
        deadWhites = this.deadWhites;
        deadBlacks = this.deadBlacks;
        chosenPieces = this.chosenPieces;
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
        cp.GetComponent<MeshRenderer>().material = Utilities.Instance.GetPieceMaterial(type, team);

        return cp;
    }

    // Positioning
    private void PositionAllPieces() {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    PositionSinglePiece(x, y, true);
    }

    public void PositionSinglePiece(int x, int y, bool force = false) {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }

    private Vector3 GetTileCenter(int x, int y) {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2,0, tileSize / 2);
    }

    // Checkmate
    public void CheckMate(int team) {
        gameStarted = false;
        if(winningTeam != 3) // Invalid action prevention
            winningTeam = team;
        GameUIManager.Instance.HandleCheckmate();
        DisplayVictory(team);
    }

    private void DisplayVictory(int winningTeam) {
        if (!onReplay) {
            SoundManager.Instance.PlayEndGameSound(winningTeam, startingTeam);
        }
        GameUIManager.Instance.HandleDisplayVictory(winningTeam);
    }

    public void OnRematchButton() {
        SoundManager.Instance.StopAudio();
        if (localGame) {
            onReplay = false;
            GameUIManager.Instance.HandleGoingOnRematch();

            NetRematch wrm = new NetRematch();
            wrm.teamId = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            NetRematch brm = new NetRematch();
            brm.teamId = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);

            GameUIManager.Instance.SetSaveMatchUI();

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
        }
        pausedForPromotion = false;
        GameUIManager.Instance.HandleGameReset(onReplay);

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
        SoundManager.Instance.StopAudio();
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);

        onMenu = true;
        onReplay = false;
        GameReset();
        GameUIManager.Instance.HandleGoingOnMenu();
        GameUI.Instance.OnLeaveFromGameMenu();

        Invoke("ShutDownRelay", 1.0f);

        // Reset some values
        playerCount = -1;
        currentTeam = -1;
        startingTeam = -1;
    }

    public void OnSurrenderButton() {
        NetSurrender sr = new NetSurrender();
        sr.winningTeam = (currentTeam == 0) ? 1 : 0;
        sr.teamId = currentTeam;
        Client.Instance.SendToServer(sr);
        CheckMate(sr.winningTeam);
        if(pausedForPromotion) GameUIManager.Instance.RemovePromotionPopup();
    }

    // Replay

    public void OnReplayButton() {
        SoundManager.Instance.StopAudio();
        if (!localGame) {
            NetRematch rm = new NetRematch();
            rm.teamId = currentTeam;
            rm.wantRematch = 0;
            Client.Instance.SendToServer(rm);

            Invoke("ShutDownRelay", 1.0f);

            localGame = true;
        }

        GameUIManager.Instance.HandleGoingOnReplay();
        onReplay = true;
        GameReset();

        OnReplayDataLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void OnSaveMatchButton() {
        GameUIManager.Instance.DisableSaveMatchUI();

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
            saveName = ReplayManager.Instance.GetSaveNameText()
        };
        ReplayManager.Instance.SaveReplay(matchReplayData);
    }

    public void OnBackToSaveMatchesButton() {
        OnMenuButton();
        GameUI.Instance.OnSavedMatchesButton();
    }

    // Special Moves
    private void ProcessSpecialMove() {
        switch (specialMove) {
            case SpecialMove.EnPassant:
                ProcessEnPassant();
                break;
            case SpecialMove.Promotion:
                ProcessPromotion();
                break;
            case SpecialMove.Castling:
                ProcessCastling();
                break;
        }
    }

    private void ProcessEnPassant() {
        Vector2Int[] newMove;
        ChessPiece myPawn, enemyPawn;
        Vector2Int[] targetPawnPosition;

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
            ReplayGameManager.Instance.ReverseEnPassant();
            return;
        }

        CaptureEnemyPawn(myPawn, enemyPawn);
    }

    private void CaptureEnemyPawn(ChessPiece myPawn, ChessPiece enemyPawn) {
        if (myPawn.currentX == enemyPawn.currentX &&
            (myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1)) {
            enemyPawn.capturedPosition = new Vector2Int(enemyPawn.currentX, enemyPawn.currentY);
            isCapture = true;

            if (onReplay) enemyPawn.capturedMove = currentReplayMove;

            AddToGraveyard(enemyPawn);
            chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
        } else {
            isCapture = false;
        }
    }

    private void AddToGraveyard(ChessPiece enemyPawn) {
        enemyPawn.capturedPosition = new Vector2Int(enemyPawn.currentX, enemyPawn.currentY);  // Store the capture position

        if (enemyPawn.team == 0) {
            deadWhites.Add(enemyPawn);
            PositionWhiteGraveyard(enemyPawn);
        } else {
            deadBlacks.Add(enemyPawn);
            PositionBlackGraveyard(enemyPawn);
        }

        chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
    }

    private void PositionWhiteGraveyard(ChessPiece piece) {
        piece.SetScale(Vector3.one * deathSize);
        piece.SetPosition(
            new Vector3(8 * tileSize, yOffset, -1 * tileSize)
            - bounds
            + new Vector3(tileSize / 2, 0, tileSize / 2)
            + (Vector3.forward * deathSpacing) * deadWhites.Count);
    }

    private void PositionBlackGraveyard(ChessPiece piece) {
        piece.SetScale(Vector3.one * deathSize);
        piece.SetPosition(
            new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
            - bounds
            + new Vector3(tileSize / 2, 0, tileSize / 2)
            + (Vector3.back * deathSpacing) * deadBlacks.Count);
    }

    private void ProcessPromotion() {
        Vector2Int[] lastMove;
        ChessPiece targetPawn;

        if (!onReplay) lastMove = moveList[moveList.Count - 1];
        else if (!goingForward) {
            currentReplayChosenPieceIndex--;
            return;
        } else lastMove = moveList[currentReplayMove];

        targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

        if (targetPawn.type == ChessPieceType.Pawn) {
            currentlyPromotingPawn = targetPawn;
            promotingPosition = lastMove[1];

            if (!onReplay) HandleLocalPromotion();
            else HandleReplayPromotion();
        }
    }

    private void HandleLocalPromotion() {
        pausedForPromotion = true;
        isWhiteTurn = !isWhiteTurn;
        currentTime = prePromotionTime;
        if (localGame) currentTeam = (currentTeam == 0) ? 1 : 0;

        if (currentlyPromotingPawn.team == currentTeam) {
            GameUIManager.Instance.SetPromotionPopup(currentlyPromotingPawn);
        }
    }

    private void HandleReplayPromotion() {
        currentReplayChosenPieceIndex++;
        try {
            OnPromotionSelected(chosenPieces[currentReplayChosenPieceIndex]);
        } catch (ArgumentOutOfRangeException) {
            ReplayManager.Instance.SetMoveText("Surrendered during promotion!");
        }
    }

    private void ProcessCastling() {
        Vector2Int[] lastMove;

        if (!onReplay) lastMove = moveList[moveList.Count - 1];
        else if (goingForward) lastMove = moveList[currentReplayMove];
        else lastMove = moveList[currentReplayMove - 1];

        if (lastMove[1].x == 2) {
            ProcessLeftRookCastling(lastMove[1].y);
        } else if (lastMove[1].x == 6) {
            ProcessRightRookCastling(lastMove[1].y);
        }
    }

    private void ProcessLeftRookCastling(int y) {
        if (goingForward) {
            MoveRookDuringCastling(0, y, 3);
        } else {
            MoveRookDuringCastling(3, y, 0);
        }
    }

    private void ProcessRightRookCastling(int y) {
        if (goingForward) {
            MoveRookDuringCastling(7, y, 5);
        } else {
            MoveRookDuringCastling(5, y, 7);
        }
    }

    private void MoveRookDuringCastling(int startX, int y, int endX) {
        ChessPiece rook = chessPieces[startX, y];
        chessPieces[endX, y] = rook;
        PositionSinglePiece(endX, y);
        chessPieces[startX, y] = null;
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
            if (targetKing.currentX != lastMove[0].x && targetKing.currentY != lastMove[0].y)
                GameUIManager.Instance.RemoveCheckHighlight(ref tiles, targetKing);
            else tiles[lastMove[0].x, lastMove[0].y].layer = LayerMask.NameToLayer("Tile");
            targetKing = null;
        }

        if (Utilities.Instance.IsInsufficientMaterial(ref chessPieces)) return 2;

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
                    GameUIManager.Instance.HandleGameWarning("CHECK!");
                    tiles[targetKing.currentX, targetKing.currentY].layer = LayerMask.NameToLayer("KingCheck");
                    if(goingForward)
                        isCheck = true;
                    return 0;
                }
            }

            GameUIManager.Instance.RemoveCheckHighlight(ref tiles, targetKing);
            GameUIManager.Instance.HandleGameWarning("CHECKMATE!");
            isCheck = false;
            return 1; // Checkmate exit
        } else {
            isCheck = false;
            if (targetKing.currentX != lastMove[0].x && targetKing.currentY != lastMove[0].y)
                GameUIManager.Instance.RemoveCheckHighlight(ref tiles, targetKing);
            else tiles[lastMove[0].x, lastMove[0].y].layer = LayerMask.NameToLayer("Tile");
            for (int i = 0; i < defendingPieces.Count; i++) {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                // Since we're sending ref defendingMoves, we will be deleting moves that are putting us in check
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0) {
                    GameUIManager.Instance.HandleGameWarning("");
                    return 0;
                }
            }

            GameUIManager.Instance.HandleGameWarning("STALEMATE!");
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

    public void MoveTo(int originalX, int originalY, int x, int y) {
        ChessPiece cp = chessPieces[originalX, originalY];
        Vector2Int previousPosition = new Vector2Int(originalX, originalY);

        if (isCheck) GameUIManager.Instance.RemoveCheckHighlight(ref tiles, targetKing);

        if (goingForward) {
            HandleCaptureIfAny(cp, x, y);
            chessPieces[previousPosition.x, previousPosition.y] = null;
        } else {
            isCheck = false;
            isCapture = false;
            RestoreCapturedPieceIfNecessary(originalX, originalY, x, y);
        }

        PlacePieceAtNewPosition(cp, x, y);

        if (onReplay) HandleReplaySpecifics(cp, previousPosition, x, y);

        if (!pausedForPromotion) {
            UpdateTurnStatus();
            if (!onReplay) moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });
        }

        ProcessSpecialMove();
        FinalizeMove(cp, x, y);

        if (currentlyDragging) currentlyDragging = null;
        GameUIManager.Instance.RemoveHighlightTiles(ref availableMoves, ref chessPieces, ref tiles);
    }

    private void HandleCaptureIfAny(ChessPiece cp, int x, int y) {
        if (chessPieces[x, y] != null) {
            ChessPiece ocp = chessPieces[x, y];
            if (cp.team != ocp.team) {
                ocp.capturedPosition = new Vector2Int(x, y);
                if (onReplay) ocp.capturedMove = currentReplayMove;

                if (ocp.team == 0) {
                    if (ocp.type == ChessPieceType.King) CheckMate(1);

                    AddToWhiteGraveyard(ocp);
                } else {
                    if (ocp.type == ChessPieceType.King) CheckMate(0);

                    AddToBlackGraveyard(ocp);
                }

                isCapture = true;
            }
        } else {
            isCapture = false;
        }
    }

    private void AddToWhiteGraveyard(ChessPiece ocp) {
        deadWhites.Add(ocp);
        ocp.SetScale(Vector3.one * deathSize);
        ocp.SetPosition(
            new Vector3(8 * tileSize, yOffset, -1 * tileSize)
            - bounds
            + new Vector3(tileSize / 2, 0, tileSize / 2)
            + (Vector3.forward * deathSpacing) * deadWhites.Count);
    }

    private void AddToBlackGraveyard(ChessPiece ocp) {
        deadBlacks.Add(ocp);
        ocp.SetScale(Vector3.one * deathSize);
        ocp.SetPosition(
            new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
            - bounds
            + new Vector3(tileSize / 2, 0, tileSize / 2)
            + (Vector3.back * deathSpacing) * deadBlacks.Count);
    }

    private void RestoreCapturedPieceIfNecessary(int originalX, int originalY, int x, int y) {
        Vector2Int previousCapturePosition = GetPreviousCapturePosition();
        if (chessPieces[x, y] == null && previousCapturePosition == new Vector2Int(originalX, originalY)) {
            ChessPiece ocp = (!isWhiteTurn) ? deadBlacks.Last() : deadWhites.Last();
            if (ocp.capturedMove == currentReplayMove - 1) {
                chessPieces[originalX, originalY] = ocp;
                PositionSinglePiece(originalX, originalY);
                ocp.SetScale(Vector3.one);
                ocp.capturedPosition = -Vector2Int.one;
                RemoveFromGraveyard(ocp);
            }
        } else {
            chessPieces[originalX, originalY] = null;
        }
    }

    private Vector2Int GetPreviousCapturePosition() {
        if ((deadWhites.Count == 0 && isWhiteTurn) || (deadBlacks.Count == 0 && !isWhiteTurn))
            return -Vector2Int.one;
        return (!isWhiteTurn) ? deadBlacks.Last().capturedPosition : deadWhites.Last().capturedPosition;
    }

    private void RemoveFromGraveyard(ChessPiece piece) {
        if (piece.team == 0) deadWhites.Remove(piece);
        else deadBlacks.Remove(piece);
    }

    private void PlacePieceAtNewPosition(ChessPiece cp, int x, int y) {
        chessPieces[x, y] = cp;
        PositionSinglePiece(x, y);
    }

    private void HandleReplaySpecifics(ChessPiece cp, Vector2Int previousPosition, int x, int y) {
        if (!goingForward && cp.promoted) {
            int index = chosenPiecesPromotionPositions.IndexOf(previousPosition);
            if (index != -1 && promotionMoveIndexList[currentReplayChosenPieceIndex] >= currentReplayMove) {
                HandlePromotionReplay(cp, x, y);
            }
        }
    }

    private void HandlePromotionReplay(ChessPiece cp, int x, int y) {
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

    private void UpdateTurnStatus() {
        isWhiteTurn = !isWhiteTurn;
        prePromotionTime = currentTime;
        currentTime = TURN_TIMER_MAX;
        if (localGame) currentTeam = (currentTeam == 0) ? 1 : 0;
    }

    private void FinalizeMove(ChessPiece cp, int x, int y) {
        if (goingForward) {
            switch (CheckForCheckmate()) {
                case 1:
                    CheckMate(cp.team);
                    break;
                case 2:
                    CheckMate(2);
                    break;
                default:
                    if (onReplay) {
                        Utilities.Instance.PlayReplaySoundEffect();
                    } else {
                        Utilities.Instance.PlayMoveSoundEffect(isCheck, specialMove, isCapture);
                        if (localGame && GameUIManager.Instance.GetLocalRotateEnabled())
                            GameUI.Instance.ChangeCamera(isWhiteTurn ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
                    }
                    break;
            }
        }
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
            GameUIManager.Instance.HandleRematchInfo(rm.wantRematch);
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
            if (pausedForPromotion) GameUIManager.Instance.RemovePromotionPopup();
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
            GameUIManager.Instance.SetRematchUI();
        }
    }

    private void OnPromotionSelected(ChessPieceType chosenPiece) {
        if (currentlyPromotingPawn == null) return;

        int team = currentlyPromotingPawn.team;
        Vector3 pawnPosition = currentlyPromotingPawn.transform.position;

        // Spawn and position the promoted piece
        ChessPiece newPiece = SpawnSinglePiece(chosenPiece, team);
        newPiece.transform.position = pawnPosition;
        Destroy(currentlyPromotingPawn.gameObject);
        chessPieces[promotingPosition.x, promotingPosition.y] = newPiece;
        PositionSinglePiece(promotingPosition.x, promotingPosition.y);

        if (goingForward) {
            RecordPromotion(newPiece, chosenPiece);
        }

        // Update game state and trigger necessary events
        FinalizePromotion();
    }

    private void RecordPromotion(ChessPiece newPiece, ChessPieceType chosenPiece) {
        newPiece.promoted = true;
        if (onReplay) return;

        chosenPieces.Add(chosenPiece);
        chosenPiecesPromotionPositions.Add(promotingPosition);
        promotionMoveIndexList.Add(moveList.Count);
    }

    private void FinalizePromotion() {
        GameUIManager.Instance.RemovePromotionPopup();
        pausedForPromotion = false;

        if (!onReplay) {
            UpdateTurnAfterPromotion();
            CheckForCheckmate();

            if (!isCheck) Utilities.Instance.TriggerPromotionSound();
        }

        SendPromotionToServer();
    }

    private void UpdateTurnAfterPromotion() {
        isWhiteTurn = !isWhiteTurn;
        prePromotionTime = currentTime;
        currentTime = TURN_TIMER_MAX;

        if (localGame) {
            if (GameUIManager.Instance.GetLocalRotateEnabled()) {
                GameUI.Instance.ChangeCamera(isWhiteTurn ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
            }
            currentTeam = (currentTeam == 0) ? 1 : 0;
        }
    }

    private void SendPromotionToServer() {
        NetPromotion pr = new NetPromotion {
            teamId = currentTeam,
            promotionIndex = (int)currentlyPromotingPawn.type
        };
        Client.Instance.SendToServer(pr);
    }

    private void ReplayManager_OnReplayLoaded(object sender, ReplayData e) {
        InitializeReplay(e.startingTeam);
        LoadReplayMoves(e.moveList);
        SetReplayData(e);

        OnReplayDataLoaded?.Invoke(this, EventArgs.Empty);
    }

    private void InitializeReplay(int startingTeam) {
        ReplayManager.Instance.DisableSavedMatchesUI();
        GameUI.Instance.OnLocalGameButton();
        onReplay = true;
        GameUI.Instance.ChangeCamera(startingTeam == 0 ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
        GameUIManager.Instance.DisableSaveMatchUI();
    }

    private void LoadReplayMoves(List<MoveData> replayMoveData) {
        List<Vector2Int[]> loadedMoveList = new List<Vector2Int[]>();

        foreach (MoveData moveData in replayMoveData) {
            loadedMoveList.Add(new[] { moveData.start, moveData.end });
        }

        moveList = loadedMoveList;
    }

    private void SetReplayData(ReplayData e) {
        chosenPieces = e.chosenPieces;
        chosenPiecesPromotionPositions = e.chosenPiecesPromotionPositions;
        promotionMoveIndexList = e.promotionMoveIndexList;
        winningTeam = e.winningTeam;
        startingTeam = e.startingTeam;
    }

    private void GameStart(bool firstStart) {
        gameStarted = true;
        GameUIManager.Instance.HandleGameWarning("");
        ReplayManager.Instance.SetSaveNameText("");
        if (firstStart)
            currentTime = TURN_TIMER_MAX + 1.5f;
        else
            currentTime = TURN_TIMER_MAX;
        GameUIManager.Instance.HandleGameStart(currentTime, localGame);
    }
    #endregion
}
