using DG.Tweening;
using UnityEngine;

public class MatchBlock : MonoBehaviour
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public int TypeIndex { get; private set; }

    private SpriteRenderer spriteRenderer;
    private MatchManager manager;
    private Vector3 defaultScale;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Setup(
        int x,
        int y,
        int typeIndex,
        Sprite sprite,
        MatchManager manager,
        Vector3 scale
    )
    {
        X = x;
        Y = y;
        TypeIndex = typeIndex;
        this.manager = manager;
        spriteRenderer.sprite = sprite;
        transform.localScale = scale;
        defaultScale = scale;
    }

    public void UpdatePosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    private void OnMouseDown()
    {
        transform.DOScale(defaultScale * 0.8f, 0.1f);
        manager.SelectBlock(this);
    }

    private void OnMouseEnter()
    {
        manager.HoverBlock(this);
    }

    private void OnMouseUp()
    {
        transform.DOScale(defaultScale, 0.1f);
        manager.ReleaseBlock();
    }

    public void MoveToPosition(Vector3 targetPos)
    {
        transform.DOMove(targetPos, 0.3f).SetEase(Ease.OutQuad);
    }

    public void DestroyBlock()
    {
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScale(defaultScale * 1.2f, 0.1f));
        seq.Append(transform.DOScale(0f, 0.15f));
        seq.OnComplete(() => Destroy(gameObject));
    }
}
