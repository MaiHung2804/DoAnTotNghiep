using UnityEngine;

public enum PieceType
{
    None,
    Pawn,
    Rook,
    Knight,
    Bishop,
    Queen,
    King
}

public enum PieceColor
{
    White,
    Black
}

[System.Serializable]
public class PieceSpriteEntry
{
    public PieceColor color;
    public PieceType pieceType;
    public Sprite sprite;
}

public struct ChessMove
{
    public int fromX;
    public int fromY;
    public int toX;
    public int toY;

    public bool isEnPassant;
    public bool isCastling;
    public bool isPromotion;

    public ChessMove(int fx, int fy, int tx, int ty)
    {
        fromX = fx;
        fromY = fy;
        toX = tx;
        toY = ty;
        isEnPassant = false;
        isCastling = false;
        isPromotion = false;
    }

    public bool Matches(int x, int y) => toX == x && toY == y;
}
