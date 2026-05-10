using UnityEngine;

public class ChessPiece : MonoBehaviour
{
    public PieceType pieceType;
    public PieceColor pieceColor;
    public int boardX;
    public int boardY;

    [HideInInspector] public bool hasMoved = false;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        EnsureSpriteRenderer();
    }

    public void SetData(PieceType type, PieceColor color, int x, int y, Sprite sprite)
    {
        pieceType = type;
        pieceColor = color;
        boardX = x;
        boardY = y;

        EnsureSpriteRenderer();
        spriteRenderer.sprite = sprite;
        RefreshName();
    }

    public void SetBoardPosition(int x, int y)
    {
        boardX = x;
        boardY = y;
        RefreshName();
    }

    private void EnsureSpriteRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void RefreshName()
    {
        name = $"{pieceColor}_{pieceType}_{boardX}_{boardY}";
    }
}
