using UnityEngine;

public class StartButton : MonoBehaviour
{
    [SerializeField] private MainManager mainManager;
    [SerializeField] private Transform player;
    [SerializeField] private float holdSeconds = 1.5f;
    [SerializeField] private float triggerRadius = 0.9f;
    [SerializeField] private SpriteRenderer progressRenderer;
    [SerializeField] private Color waitingColor = Color.white;
    [SerializeField] private Color holdingColor = new Color(0.75f, 1f, 0.75f, 1f);

    private float holdTimer;
    private SpriteRenderer ownRenderer;

    private void Awake()
    {
        ownRenderer = GetComponent<SpriteRenderer>();
        if (mainManager == null)
            mainManager = FindFirstObjectByType<MainManager>();
    }

    private void Update()
    {
        if (player == null || mainManager == null)
            return;

        bool isHolding = IsPlayerOnButton();

        holdTimer = isHolding
            ? Mathf.Min(holdSeconds, holdTimer + Time.deltaTime)
            : 0f;

        UpdateVisual(isHolding);

        if (holdTimer >= holdSeconds)
        {
            mainManager.StartGame();
        }
    }

    public void Show()
    {
        holdTimer = 0f;
        gameObject.SetActive(true);
        UpdateVisual(false);
    }

    public void Hide()
    {
        holdTimer = 0f;
        gameObject.SetActive(false);
    }

    private void UpdateVisual(bool isHolding)
    {
        if (ownRenderer != null)
            ownRenderer.color = isHolding ? holdingColor : waitingColor;

        if (progressRenderer != null)
        {
            float progress = holdSeconds > 0f ? Mathf.Clamp01(holdTimer / holdSeconds) : 1f;
            progressRenderer.transform.localScale = new Vector3(progress, progress, 1f);
        }
    }

    private bool IsPlayerOnButton()
    {
        Vector2 playerPosition = player.position;

        if (ownRenderer != null && ownRenderer.sprite != null)
        {
            Bounds bounds = ownRenderer.bounds;
            return playerPosition.x >= bounds.min.x
                && playerPosition.x <= bounds.max.x
                && playerPosition.y >= bounds.min.y
                && playerPosition.y <= bounds.max.y;
        }

        Vector2 diff = playerPosition - (Vector2)transform.position;
        return diff.sqrMagnitude <= triggerRadius * triggerRadius;
    }
}
