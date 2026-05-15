using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Photon.Pun;

public class ChessGame : MonoBehaviourPun
{
    private const int BoardSize = 8;

    private static readonly Vector2Int[] RookDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] BishopDirections =
    {
        new Vector2Int(1, 1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1)
    };

    private static readonly Vector2Int[] QueenDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1)
    };

    private static readonly Vector2Int[] KnightOffsets =
    {
        new Vector2Int(1, 2),
        new Vector2Int(2, 1),
        new Vector2Int(2, -1),
        new Vector2Int(1, -2),
        new Vector2Int(-1, -2),
        new Vector2Int(-2, -1),
        new Vector2Int(-2, 1),
        new Vector2Int(-1, 2)
    };

    private static readonly int[] PawnCaptureOffsets = { -1, 1 };
    private static readonly int[] KingSideCastlingSquares = { 5, 6 };
    private static readonly int[] QueenSideCastlingSquares = { 3, 2 };

    [Header("Board")]
    public Transform piecesRoot;
    public GameObject chessPiecePrefab;

    [Header("Board Layout")]
    public float tileSize = 1f;
    public Vector2 boardOrigin = new Vector2(-3.5f, -3.5f);
    public float pieceZ = -1f;

    [Header("Sprites")]
    public List<PieceSpriteEntry> pieceSprites = new List<PieceSpriteEntry>();

    [Header("AI")]
    public bool playVsStockfish = true;
    public PieceColor humanColor = PieceColor.White;
    public StockfishManager stockfishManager;

    [Header("Highlight")]
    public GameObject tileHighlightPrefab; // gán TileHighlightPrefab
    private List<GameObject> activeHighlights = new List<GameObject>();

    private ChessPiece[,] board = new ChessPiece[BoardSize, BoardSize];
    private readonly Dictionary<(PieceColor, PieceType), Sprite> spriteLookup =
        new Dictionary<(PieceColor, PieceType), Sprite>();

    private PieceColor currentTurn = PieceColor.White;
    private ChessPiece selectedPiece;
    private readonly List<ChessMove> currentLegalMoves = new List<ChessMove>();

    private Vector2Int? enPassantTarget = null;
    private int halfmoveClock = 0;
    private int fullmoveNumber = 1;

    private int gameMode = 0; // 0 = AI, 1 = Local 2 players, 2 = Online
    private PieceColor playerColor = PieceColor.White;
    private bool aiThinking = false;
    private bool isGameOver = false;
    private bool isBoardFlipped = false;

    private void Start()
    {
        gameMode = PlayerPrefs.GetInt("GameMode", 0);
        SetupGameMode();

        if (piecesRoot == null)
        {
            Debug.LogError("ChessGame: piecesRoot chưa được gán.");
            return;
        }

        if (chessPiecePrefab == null)
        {
            Debug.LogError("ChessGame: chessPiecePrefab chưa được gán.");
            return;
        }

        BuildSpriteLookup();
        SpawnInitialPieces();
        RefreshAllPiecePositions();

        MaybeStartAIMove();
    }

    private void Update()
    {
        if (isGameOver) return;
        if (aiThinking) return;
        if (!IsHumanTurn()) return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    private void SetupGameMode()
    {
        isGameOver = false;
        isBoardFlipped = false;
        playerColor = PieceColor.White;

        if (gameMode == 2)
        {
            playVsStockfish = false;
            playerColor = PhotonNetwork.IsMasterClient ? PieceColor.White : PieceColor.Black;

            if (playerColor == PieceColor.Black)
            {
                isBoardFlipped = true;
                //FlipCamera();
            }

            Debug.Log($">> VÀO GAME: ONLINE - Bạn là {playerColor}");
        }
        else if (gameMode == 1)
        {
            playVsStockfish = false;
            Debug.Log(">> VÀO GAME: NGƯỜI VS NGƯỜI LOCAL");
        }
        else
        {
            playVsStockfish = true;
            Debug.Log(">> VÀO GAME: NGƯỜI VS MÁY - Elo: " + PlayerPrefs.GetInt("SelectedElo", 550));
        }
    }


    private bool IsHumanTurn()
    {
        if (gameMode == 2)
        {
            return currentTurn == playerColor;
        }

        if (!playVsStockfish)
        {
            return true;
        }

        return currentTurn == humanColor;
    }

    private void BuildSpriteLookup()
    {
        spriteLookup.Clear();

        foreach (var entry in pieceSprites)
        {
            var key = (entry.color, entry.pieceType);
            if (!spriteLookup.ContainsKey(key))
            {
                spriteLookup.Add(key, entry.sprite);
            }
        }
    }

    private Sprite GetPieceSprite(PieceColor color, PieceType type)
    {
        var key = (color, type);
        return spriteLookup.TryGetValue(key, out Sprite sprite) ? sprite : null;
    }

    private void SpawnInitialPieces()
    {
        SpawnMajorPieces(PieceColor.White, 0);
        SpawnPawns(PieceColor.White, 1);

        SpawnMajorPieces(PieceColor.Black, BoardSize - 1);
        SpawnPawns(PieceColor.Black, BoardSize - 2);
    }

    private void SpawnMajorPieces(PieceColor color, int y)
    {
        SpawnPiece(PieceType.Rook, color, 0, y);
        SpawnPiece(PieceType.Knight, color, 1, y);
        SpawnPiece(PieceType.Bishop, color, 2, y);
        SpawnPiece(PieceType.Queen, color, 3, y);
        SpawnPiece(PieceType.King, color, 4, y);
        SpawnPiece(PieceType.Bishop, color, 5, y);
        SpawnPiece(PieceType.Knight, color, 6, y);
        SpawnPiece(PieceType.Rook, color, 7, y);
    }

    private void SpawnPawns(PieceColor color, int y)
    {
        for (int x = 0; x < BoardSize; x++)
        {
            SpawnPiece(PieceType.Pawn, color, x, y);
        }
    }

    private ChessPiece SpawnPiece(PieceType type, PieceColor color, int x, int y)
    {
        GameObject obj = Instantiate(chessPiecePrefab, piecesRoot);
        obj.transform.position = GetWorldPosition(x, y, pieceZ);

        ChessPiece piece = obj.GetComponent<ChessPiece>();
        if (piece == null)
        {
            Debug.LogError("ChessGame: chessPiecePrefab thiếu component ChessPiece.");
            Destroy(obj);
            return null;
        }

        piece.SetData(type, color, x, y, GetPieceSprite(color, type));
        board[x, y] = piece;
        return piece;
    }

    private void HandleMouseClick()
    {
        if (Camera.main == null) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        if (TryGetClickedPiece(mousePos, out ChessPiece hitPiece))
        {
            OnPieceClicked(hitPiece);
            return;
        }

        Vector2Int tile = WorldToBoard(mousePos);
        if (InBounds(tile.x, tile.y))
        {
            OnBoardCellClicked(tile.x, tile.y);
        }
    }

    private void OnPieceClicked(ChessPiece piece)
    {
        if (piece.pieceColor == currentTurn)
        {
            SelectPiece(piece);
            return;
        }

        if (selectedPiece != null)
        {
            TryMoveSelectedPiece(piece.boardX, piece.boardY);
        }
    }

    private void OnBoardCellClicked(int x, int y)
    {
        if (selectedPiece != null)
        {
            TryMoveSelectedPiece(x, y);
        }
    }

    private void SelectPiece(ChessPiece piece)
    {
        selectedPiece = piece;
        currentLegalMoves.Clear();
        currentLegalMoves.AddRange(GetLegalMoves(piece));
        Debug.Log($"Selected: {piece.name}, legal moves: {currentLegalMoves.Count}");

        ClearHighlights();
        foreach (ChessMove move in currentLegalMoves)
        {
            Vector3 pos = GetWorldPosition(move.toX, move.toY, pieceZ - 0.1f); // đặt dưới quân
            GameObject highlight = Instantiate(tileHighlightPrefab, piecesRoot);
            highlight.transform.position = pos;
            highlight.transform.localScale = Vector3.one * tileSize; // đúng kích thước ô
            activeHighlights.Add(highlight);
        }
    }

    private void ClearHighlights()
    {
        foreach (GameObject go in activeHighlights)
        {
            Destroy(go);
        }
        activeHighlights.Clear();
    }

    private void ClearSelection()
    {
        selectedPiece = null;
        currentLegalMoves.Clear();
        ClearHighlights() ;
    }

    private void TryMoveSelectedPiece(int targetX, int targetY)
    {
        if (!InBounds(targetX, targetY)) return;

        if (TryGetLegalMove(targetX, targetY, out ChessMove selectedMove))
        {
            ExecuteMove(selectedMove, true);

            if (gameMode == 2)
            {
                photonView.RPC("RPC_ReceiveOnlineMove", RpcTarget.Others, ConvertMoveToUci(selectedMove));
            }

            ClearSelection();
            MaybeStartAIMove();
            return;
        }

        ChessPiece targetPiece = board[targetX, targetY];
        if (targetPiece != null && targetPiece.pieceColor == currentTurn)
        {
            SelectPiece(targetPiece);
        }
    }

    private bool TryGetLegalMove(int targetX, int targetY, out ChessMove selectedMove)
    {
        foreach (ChessMove move in currentLegalMoves)
        {
            if (move.Matches(targetX, targetY))
            {
                selectedMove = move;
                return true;
            }
        }

        selectedMove = default;
        return false;
    }

    public void ExecuteMove(ChessMove move, bool realMove)
    {
        ChessPiece movingPiece = board[move.fromX, move.fromY];
        if (movingPiece == null) return;

        ChessPiece capturedPiece = CapturePieceForMove(movingPiece, move, realMove);
        bool wasCapture = capturedPiece != null;
        bool wasPawnMove = movingPiece.pieceType == PieceType.Pawn;

        MovePiece(movingPiece, move, realMove);

        if (move.isCastling)
        {
            MoveCastlingRook(move, realMove);
        }

        UpdateEnPassantTarget(movingPiece, move);
        PromotePawnIfNeeded(movingPiece, move, realMove);

        if (!realMove) return;

        UpdateMoveCounters(wasPawnMove, wasCapture);
        SwitchTurn();
        CheckGameState();
    }

    private ChessPiece CapturePieceForMove(ChessPiece movingPiece, ChessMove move, bool realMove)
    {
        if (!move.isEnPassant)
        {
            return CapturePieceAt(move.toX, move.toY, realMove);
        }

        int capturedPawnY = movingPiece.pieceColor == PieceColor.White ? move.toY - 1 : move.toY + 1;
        return CapturePieceAt(move.toX, capturedPawnY, realMove);
    }

    private ChessPiece CapturePieceAt(int x, int y, bool realMove)
    {
        ChessPiece capturedPiece = board[x, y];

        if (capturedPiece != null && realMove)
        {
            Destroy(capturedPiece.gameObject);
        }

        board[x, y] = null;
        return capturedPiece;
    }

    private void MovePiece(ChessPiece piece, ChessMove move, bool realMove)
    {
        board[move.fromX, move.fromY] = null;
        board[move.toX, move.toY] = piece;

        piece.SetBoardPosition(move.toX, move.toY);
        piece.hasMoved = true;

        if (realMove)
        {
            piece.transform.position = GetWorldPosition(move.toX, move.toY, pieceZ);
        }
    }

    private void MoveCastlingRook(ChessMove move, bool realMove)
    {
        if (move.toX == 6)
        {
            RepositionRookForCastling(BoardSize - 1, 5, move.fromY, realMove);
        }
        else if (move.toX == 2)
        {
            RepositionRookForCastling(0, 3, move.fromY, realMove);
        }
    }

    private void RepositionRookForCastling(int fromX, int toX, int y, bool realMove)
    {
        ChessPiece rook = board[fromX, y];
        if (rook == null) return;

        board[fromX, y] = null;
        board[toX, y] = rook;

        rook.SetBoardPosition(toX, y);
        rook.hasMoved = true;

        if (realMove)
        {
            rook.transform.position = GetWorldPosition(toX, y, pieceZ);
        }
    }

    private void UpdateEnPassantTarget(ChessPiece movingPiece, ChessMove move)
    {
        enPassantTarget = null;

        if (movingPiece.pieceType != PieceType.Pawn) return;
        if (Mathf.Abs(move.toY - move.fromY) != 2) return;

        int midY = (move.fromY + move.toY) / 2;
        enPassantTarget = new Vector2Int(move.fromX, midY);
    }

    private void PromotePawnIfNeeded(ChessPiece piece, ChessMove move, bool realMove)
    {
        if (piece.pieceType != PieceType.Pawn) return;
        if (!IsPromotionRank(piece.pieceColor, move.toY)) return;

        ApplyPromotion(piece, PieceType.Queen, realMove);
    }

    private bool IsPromotionRank(PieceColor color, int y)
    {
        return (color == PieceColor.White && y == BoardSize - 1) ||
               (color == PieceColor.Black && y == 0);
    }

    private void ApplyPromotion(ChessPiece piece, PieceType promotionType, bool updateSprite)
    {
        piece.pieceType = promotionType;

        if (!updateSprite) return;

        SpriteRenderer spriteRenderer = piece.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = GetPieceSprite(piece.pieceColor, promotionType);
        }
    }

    private void SwitchTurn()
    {
        currentTurn = currentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;
        Debug.Log("Turn: " + currentTurn);
    }

    private void CheckGameState()
    {
        bool inCheck = IsKingInCheck(currentTurn);
        bool hasAnyMove = HasAnyLegalMove(currentTurn);

        if (!hasAnyMove)
        {
            isGameOver = true;
            Debug.Log(inCheck
                ? $"Checkmate - {(currentTurn == PieceColor.White ? "Black" : "White")} wins"
                : "Stalemate");
        }
        else if (inCheck)
        {
            Debug.Log($"{currentTurn} king is in check");
        }
    }

    private bool HasAnyLegalMove(PieceColor color)
    {
        foreach (ChessPiece piece in board)
        {
            if (piece == null) continue;
            if (piece.pieceColor != color) continue;

            if (GetLegalMoves(piece).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private List<ChessMove> GetLegalMoves(ChessPiece piece)
    {
        List<ChessMove> pseudoMoves = GetPseudoLegalMoves(piece);
        List<ChessMove> legalMoves = new List<ChessMove>();

        foreach (ChessMove move in pseudoMoves)
        {
            BoardStateSnapshot snapshot = CreateSnapshot();
            ExecuteMove(move, false);

            bool ownKingInCheck = IsKingInCheck(piece.pieceColor);

            RestoreSnapshot(snapshot);

            if (!ownKingInCheck)
            {
                legalMoves.Add(move);
            }
        }

        return legalMoves;
    }

    private List<ChessMove> GetPseudoLegalMoves(ChessPiece piece)
    {
        List<ChessMove> moves = new List<ChessMove>();

        switch (piece.pieceType)
        {
            case PieceType.Pawn:
                AddPawnMoves(piece, moves);
                break;
            case PieceType.Rook:
                AddSlidingMoves(piece, moves, RookDirections);
                break;
            case PieceType.Bishop:
                AddSlidingMoves(piece, moves, BishopDirections);
                break;
            case PieceType.Queen:
                AddSlidingMoves(piece, moves, QueenDirections);
                break;
            case PieceType.Knight:
                AddKnightMoves(piece, moves);
                break;
            case PieceType.King:
                AddKingMoves(piece, moves);
                break;
        }

        return moves;
    }

    private void AddPawnMoves(ChessPiece piece, List<ChessMove> moves)
    {
        int dir = piece.pieceColor == PieceColor.White ? 1 : -1;
        int startRow = piece.pieceColor == PieceColor.White ? 1 : 6;

        int x = piece.boardX;
        int y = piece.boardY;

        int oneStepY = y + dir;
        if (InBounds(x, oneStepY) && board[x, oneStepY] == null)
        {
            ChessMove move = new ChessMove(x, y, x, oneStepY);
            if (IsPromotionRank(piece.pieceColor, oneStepY))
            {
                move.isPromotion = true;
            }
            moves.Add(move);

            int twoStepY = y + dir * 2;
            if (y == startRow && InBounds(x, twoStepY) && board[x, twoStepY] == null)
            {
                moves.Add(new ChessMove(x, y, x, twoStepY));
            }
        }

        foreach (int dx in PawnCaptureOffsets)
        {
            int tx = x + dx;
            int ty = y + dir;

            if (!InBounds(tx, ty)) continue;

            ChessPiece target = board[tx, ty];
            if (target != null && target.pieceColor != piece.pieceColor)
            {
                ChessMove move = new ChessMove(x, y, tx, ty);
                if (IsPromotionRank(piece.pieceColor, ty))
                {
                    move.isPromotion = true;
                }
                moves.Add(move);
            }
        }

        if (enPassantTarget.HasValue)
        {
            Vector2Int ep = enPassantTarget.Value;
            if (ep.y == y + dir && Mathf.Abs(ep.x - x) == 1)
            {
                ChessMove epMove = new ChessMove(x, y, ep.x, ep.y);
                epMove.isEnPassant = true;
                moves.Add(epMove);
            }
        }
    }

    private void AddSlidingMoves(ChessPiece piece, List<ChessMove> moves, Vector2Int[] directions)
    {
        foreach (Vector2Int dir in directions)
        {
            int x = piece.boardX + dir.x;
            int y = piece.boardY + dir.y;

            while (InBounds(x, y))
            {
                ChessPiece target = board[x, y];

                if (target == null)
                {
                    moves.Add(new ChessMove(piece.boardX, piece.boardY, x, y));
                }
                else
                {
                    if (target.pieceColor != piece.pieceColor)
                    {
                        moves.Add(new ChessMove(piece.boardX, piece.boardY, x, y));
                    }
                    break;
                }

                x += dir.x;
                y += dir.y;
            }
        }
    }

    private void AddKnightMoves(ChessPiece piece, List<ChessMove> moves)
    {
        foreach (Vector2Int offset in KnightOffsets)
        {
            int x = piece.boardX + offset.x;
            int y = piece.boardY + offset.y;

            if (!InBounds(x, y)) continue;

            ChessPiece target = board[x, y];
            if (target == null || target.pieceColor != piece.pieceColor)
            {
                moves.Add(new ChessMove(piece.boardX, piece.boardY, x, y));
            }
        }
    }

    private void AddKingMoves(ChessPiece piece, List<ChessMove> moves)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int x = piece.boardX + dx;
                int y = piece.boardY + dy;

                if (!InBounds(x, y)) continue;

                ChessPiece target = board[x, y];
                if (target == null || target.pieceColor != piece.pieceColor)
                {
                    moves.Add(new ChessMove(piece.boardX, piece.boardY, x, y));
                }
            }
        }

        if (!piece.hasMoved && !IsKingInCheck(piece.pieceColor))
        {
            TryAddCastling(piece, moves, true);
            TryAddCastling(piece, moves, false);
        }
    }

    private void TryAddCastling(ChessPiece king, List<ChessMove> moves, bool kingSide)
    {
        int y = king.boardY;
        int rookX = kingSide ? BoardSize - 1 : 0;
        ChessPiece rook = board[rookX, y];

        if (rook == null) return;
        if (rook.pieceType != PieceType.Rook) return;
        if (rook.pieceColor != king.pieceColor) return;
        if (rook.hasMoved) return;

        int step = kingSide ? 1 : -1;
        int endX = kingSide ? 6 : 2;

        for (int x = king.boardX + step; kingSide ? x < rookX : x > rookX; x += step)
        {
            if (board[x, y] != null) return;
        }

        int[] squaresToCheck = kingSide ? KingSideCastlingSquares : QueenSideCastlingSquares;
        PieceColor enemy = OpponentColor(king.pieceColor);

        foreach (int checkX in squaresToCheck)
        {
            if (IsSquareAttacked(checkX, y, enemy)) return;
        }

        ChessMove castleMove = new ChessMove(king.boardX, y, endX, y);
        castleMove.isCastling = true;
        moves.Add(castleMove);
    }

    private bool IsKingInCheck(PieceColor color)
    {
        ChessPiece king = FindKing(color);
        if (king == null) return false;

        return IsSquareAttacked(king.boardX, king.boardY, OpponentColor(color));
    }

    private ChessPiece FindKing(PieceColor color)
    {
        foreach (ChessPiece piece in board)
        {
            if (piece == null) continue;
            if (piece.pieceColor == color && piece.pieceType == PieceType.King)
            {
                return piece;
            }
        }

        return null;
    }

    private bool IsSquareAttacked(int targetX, int targetY, PieceColor attackerColor)
    {
        foreach (ChessPiece piece in board)
        {
            if (piece == null) continue;
            if (piece.pieceColor != attackerColor) continue;

            if (PieceAttacksSquare(piece, targetX, targetY))
            {
                return true;
            }
        }

        return false;
    }

    private bool PieceAttacksSquare(ChessPiece piece, int targetX, int targetY)
    {
        int dx = targetX - piece.boardX;
        int dy = targetY - piece.boardY;

        switch (piece.pieceType)
        {
            case PieceType.Pawn:
                {
                    int dir = piece.pieceColor == PieceColor.White ? 1 : -1;
                    return dy == dir && Mathf.Abs(dx) == 1;
                }

            case PieceType.Knight:
                foreach (Vector2Int offset in KnightOffsets)
                {
                    if (dx == offset.x && dy == offset.y) return true;
                }
                return false;

            case PieceType.King:
                return Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1 && (dx != 0 || dy != 0);

            case PieceType.Rook:
                return AttacksAlongDirections(piece, targetX, targetY, RookDirections);

            case PieceType.Bishop:
                return AttacksAlongDirections(piece, targetX, targetY, BishopDirections);

            case PieceType.Queen:
                return AttacksAlongDirections(piece, targetX, targetY, QueenDirections);

            default:
                return false;
        }
    }

    private bool AttacksAlongDirections(ChessPiece piece, int targetX, int targetY, Vector2Int[] directions)
    {
        foreach (Vector2Int dir in directions)
        {
            int x = piece.boardX + dir.x;
            int y = piece.boardY + dir.y;

            while (InBounds(x, y))
            {
                if (x == targetX && y == targetY)
                {
                    return true;
                }

                if (board[x, y] != null)
                {
                    break;
                }

                x += dir.x;
                y += dir.y;
            }
        }

        return false;
    }

    private void MaybeStartAIMove()
    {
        if (!playVsStockfish) return;
        if (stockfishManager == null) return;
        if (!stockfishManager.IsRunning) return;
        if (aiThinking) return;
        if (isGameOver) return;

        PieceColor aiColor = humanColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
        if (currentTurn != aiColor) return;

        aiThinking = true;
        Invoke(nameof(RequestAndPlayAIMove), 0.25f);
    }

    private void RequestAndPlayAIMove()
    {
        try
        {
            int targetElo = PlayerPrefs.GetInt("SelectedElo", 500);
            int thinkTimeMs = targetElo <= 300 ? 100 : 800;
            string fen = ExportFen();

            stockfishManager.SendCommand($"position fen {fen}");
            string bestMove = stockfishManager.GetBestMoveFromFen(fen, thinkTimeMs);

            if (!string.IsNullOrEmpty(bestMove) && bestMove != "(none)")
            {
                Debug.Log("AI đã chọn nước đi: " + bestMove);
                ApplyUciMove(bestMove);
            }
        }
        finally
        {
            aiThinking = false;
        }
    }

    [PunRPC]
    public void RPC_ReceiveOnlineMove(string uciMove)
    {
        Debug.Log(">> Nhận nước đi từ đối thủ: " + uciMove);
        ApplyUciMove(uciMove);
    }

    private void ApplyUciMove(string uciMove)
    {
        if (string.IsNullOrEmpty(uciMove) || uciMove.Length < 4)
        {
            Debug.LogError("UCI move không hợp lệ: " + uciMove);
            return;
        }

        int fromX = uciMove[0] - 'a';
        int fromY = uciMove[1] - '1';
        int toX = uciMove[2] - 'a';
        int toY = uciMove[3] - '1';

        if (!InBounds(fromX, fromY) || !InBounds(toX, toY))
        {
            Debug.LogError("UCI move out of bounds: " + uciMove);
            return;
        }

        ChessPiece piece = board[fromX, fromY];
        if (piece == null)
        {
            Debug.LogError("Không có quân ở ô bắt đầu: " + uciMove);
            return;
        }

        List<ChessMove> legalMoves = GetLegalMoves(piece);

        foreach (ChessMove move in legalMoves)
        {
            if (move.fromX == fromX && move.fromY == fromY && move.toX == toX && move.toY == toY)
            {
                ExecuteMove(move, true);

                if (uciMove.Length >= 5)
                {
                    ChessPiece promotedPiece = board[toX, toY];
                    if (promotedPiece != null)
                    {
                        ApplyPromotion(promotedPiece, GetPromotionTypeFromUci(uciMove[4]), true);
                    }
                }

                MaybeStartAIMove();
                return;
            }
        }

        Debug.LogError("Không tìm thấy legal move khớp với UCI move: " + uciMove);
    }

    private string ConvertMoveToUci(ChessMove move)
    {
        char fromFile = (char)('a' + move.fromX);
        char fromRank = (char)('1' + move.fromY);
        char toFile = (char)('a' + move.toX);
        char toRank = (char)('1' + move.toY);
        string uci = $"{fromFile}{fromRank}{toFile}{toRank}";

        if (move.isPromotion)
        {
            uci += "q";
        }

        return uci;
    }

    private PieceType GetPromotionTypeFromUci(char promotionChar)
    {
        switch (char.ToLowerInvariant(promotionChar))
        {
            case 'r': return PieceType.Rook;
            case 'b': return PieceType.Bishop;
            case 'n': return PieceType.Knight;
            case 'q':
            default:
                return PieceType.Queen;
        }
    }

    public string ExportFen()
    {
        StringBuilder sb = new StringBuilder();

        for (int y = BoardSize - 1; y >= 0; y--)
        {
            int emptyCount = 0;

            for (int x = 0; x < BoardSize; x++)
            {
                ChessPiece piece = board[x, y];

                if (piece == null)
                {
                    emptyCount++;
                }
                else
                {
                    if (emptyCount > 0)
                    {
                        sb.Append(emptyCount);
                        emptyCount = 0;
                    }

                    sb.Append(GetFenChar(piece));
                }
            }

            if (emptyCount > 0)
            {
                sb.Append(emptyCount);
            }

            if (y > 0)
            {
                sb.Append('/');
            }
        }

        sb.Append(' ');
        sb.Append(currentTurn == PieceColor.White ? 'w' : 'b');

        sb.Append(' ');
        sb.Append(GetCastlingRightsFen());

        sb.Append(' ');
        sb.Append(GetEnPassantFen());

        sb.Append(' ');
        sb.Append(halfmoveClock);

        sb.Append(' ');
        sb.Append(fullmoveNumber);

        return sb.ToString();
    }

    private char GetFenChar(ChessPiece piece)
    {
        char c = ' ';

        switch (piece.pieceType)
        {
            case PieceType.Pawn: c = 'p'; break;
            case PieceType.Rook: c = 'r'; break;
            case PieceType.Knight: c = 'n'; break;
            case PieceType.Bishop: c = 'b'; break;
            case PieceType.Queen: c = 'q'; break;
            case PieceType.King: c = 'k'; break;
        }

        return piece.pieceColor == PieceColor.White ? char.ToUpper(c) : c;
    }

    private string GetCastlingRightsFen()
    {
        string rights = "";

        ChessPiece whiteKing = FindKing(PieceColor.White);
        ChessPiece blackKing = FindKing(PieceColor.Black);

        if (whiteKing != null && !whiteKing.hasMoved)
        {
            ChessPiece rookH = board[BoardSize - 1, 0];
            ChessPiece rookA = board[0, 0];

            if (rookH != null && rookH.pieceType == PieceType.Rook && rookH.pieceColor == PieceColor.White && !rookH.hasMoved)
            {
                rights += "K";
            }

            if (rookA != null && rookA.pieceType == PieceType.Rook && rookA.pieceColor == PieceColor.White && !rookA.hasMoved)
            {
                rights += "Q";
            }
        }

        if (blackKing != null && !blackKing.hasMoved)
        {
            ChessPiece rookH = board[BoardSize - 1, BoardSize - 1];
            ChessPiece rookA = board[0, BoardSize - 1];

            if (rookH != null && rookH.pieceType == PieceType.Rook && rookH.pieceColor == PieceColor.Black && !rookH.hasMoved)
            {
                rights += "k";
            }

            if (rookA != null && rookA.pieceType == PieceType.Rook && rookA.pieceColor == PieceColor.Black && !rookA.hasMoved)
            {
                rights += "q";
            }
        }

        return string.IsNullOrEmpty(rights) ? "-" : rights;
    }

    private string GetEnPassantFen()
    {
        if (!enPassantTarget.HasValue)
        {
            return "-";
        }

        Vector2Int ep = enPassantTarget.Value;
        char file = (char)('a' + ep.x);
        char rank = (char)('1' + ep.y);
        return $"{file}{rank}";
    }

    private void UpdateMoveCounters(bool wasPawnMove, bool wasCapture)
    {
        halfmoveClock = (wasPawnMove || wasCapture) ? 0 : halfmoveClock + 1;

        if (currentTurn == PieceColor.Black)
        {
            fullmoveNumber++;
        }
    }

    private BoardStateSnapshot CreateSnapshot()
    {
        BoardStateSnapshot snapshot = new BoardStateSnapshot
        {
            board = new PieceState[BoardSize, BoardSize],
            enPassantTarget = enPassantTarget,
            currentTurn = currentTurn,
            halfmoveClock = halfmoveClock,
            fullmoveNumber = fullmoveNumber,
            isGameOver = isGameOver
        };

        for (int x = 0; x < BoardSize; x++)
        {
            for (int y = 0; y < BoardSize; y++)
            {
                ChessPiece piece = board[x, y];
                if (piece != null)
                {
                    snapshot.board[x, y] = new PieceState
                    {
                        piece = piece,
                        x = piece.boardX,
                        y = piece.boardY,
                        hasMoved = piece.hasMoved,
                        pieceType = piece.pieceType
                    };
                }
            }
        }

        return snapshot;
    }

    private void RestoreSnapshot(BoardStateSnapshot snapshot)
    {
        board = new ChessPiece[BoardSize, BoardSize];
        enPassantTarget = snapshot.enPassantTarget;
        currentTurn = snapshot.currentTurn;
        halfmoveClock = snapshot.halfmoveClock;
        fullmoveNumber = snapshot.fullmoveNumber;
        isGameOver = snapshot.isGameOver;

        for (int x = 0; x < BoardSize; x++)
        {
            for (int y = 0; y < BoardSize; y++)
            {
                PieceState state = snapshot.board[x, y];
                if (state.piece != null)
                {
                    state.piece.boardX = state.x;
                    state.piece.boardY = state.y;
                    state.piece.hasMoved = state.hasMoved;
                    state.piece.pieceType = state.pieceType;
                    board[state.x, state.y] = state.piece;
                }
            }
        }
    }

    private bool TryGetClickedPiece(Vector2 mousePos, out ChessPiece hitPiece)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(mousePos);
        foreach (Collider2D hit in hits)
        {
            ChessPiece piece = hit.GetComponent<ChessPiece>();
            if (piece == null) continue;

            hitPiece = piece;
            return true;
        }

        hitPiece = null;
        return false;
    }

    private PieceColor OpponentColor(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    private bool InBounds(int x, int y)
    {
        return x >= 0 && x < BoardSize && y >= 0 && y < BoardSize;
    }

    private Vector3 GetWorldPosition(int x, int y, float z)
    {
        if (!isBoardFlipped)
        {
            return new Vector3(
                boardOrigin.x + x * tileSize,
                boardOrigin.y + y * tileSize,
                z
            );
        }

        return new Vector3(
            boardOrigin.x + (BoardSize - 1 - x) * tileSize,
            boardOrigin.y + (BoardSize - 1 - y) * tileSize,
            z
        );
    }

    private Vector2Int WorldToBoard(Vector2 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x - boardOrigin.x) / tileSize);
        int y = Mathf.RoundToInt((worldPos.y - boardOrigin.y) / tileSize);

        if (isBoardFlipped)
        {
            x = BoardSize - 1 - x;
            y = BoardSize - 1 - y;
        }

        return new Vector2Int(x, y);
    }

    private void RefreshAllPiecePositions()
    {
        foreach (ChessPiece piece in board)
        {
            if (piece == null) continue;
            piece.transform.position = GetWorldPosition(piece.boardX, piece.boardY, pieceZ);
        }
    }

    private class BoardStateSnapshot
    {
        public PieceState[,] board;
        public Vector2Int? enPassantTarget;
        public PieceColor currentTurn;
        public int halfmoveClock;
        public int fullmoveNumber;
        public bool isGameOver;
    }

    private struct PieceState
    {
        public ChessPiece piece;
        public int x;
        public int y;
        public bool hasMoved;
        public PieceType pieceType;
    }
}
