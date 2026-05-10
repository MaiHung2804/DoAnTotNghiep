using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BoardTile : MonoBehaviour
{
    public int x;
    public int y;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        EnsureSpriteRenderer();
    }

    public void Init(int tileX, int tileY)
    {
        x = tileX;
        y = tileY;
    }

    public void SetHighlight(bool active, Color color)
    {
        EnsureSpriteRenderer();

        spriteRenderer.enabled = active;
        spriteRenderer.color = color;
    }

    private void EnsureSpriteRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
