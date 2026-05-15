using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class StartButton : MonoBehaviour
{
    [SerializeField] private MainManager mainManager;
    [SerializeField] private SoundManager soundManager;
    [SerializeField] private Transform player;
    [SerializeField] private float holdSeconds = 1.5f;
    [SerializeField] private float triggerRadius = 0.9f;
    [SerializeField] private float buttonZ = -1.4f;
    [SerializeField] private int buttonSortingOrder = -10;
    [SerializeField] private float hoverScaleMultiplier = 1.12f;
    [SerializeField] private float scaleLerpSpeed = 12f;
    [FormerlySerializedAs("hideShrinkDuration")]
    [SerializeField] private float hideGrowFadeDuration = 0.35f;
    [SerializeField] private float hideGrowMultiplier = 1.8f;
    [SerializeField] private SpriteRenderer progressRenderer;
    [SerializeField] private Color waitingColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    private float holdTimer;
    private SpriteRenderer ownRenderer;
    private Vector3 originalScale;
    private bool isHiding;

    private void Awake()
    {
        originalScale = transform.localScale;
        ownRenderer = GetComponent<SpriteRenderer>();
        if (mainManager == null)
            mainManager = FindFirstObjectByType<MainManager>();
        if (soundManager == null)
            soundManager = SoundManager.GetOrCreate();

        ApplyZPosition();
        ApplySortingOrder();
    }

    private void Update()
    {
        if (isHiding)
            return;

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

    private void OnDisable()
    {
        StopHoldSound();
    }

    public void Show()
    {
        StopHoldSound();
        isHiding = false;
        holdTimer = 0f;
        transform.localScale = originalScale;
        ApplyZPosition();
        ApplySortingOrder();
        gameObject.SetActive(true);
        SetRendererAlpha(1f);
        UpdateVisual(false);
    }

    public void Hide()
    {
        StopHoldSound();
        isHiding = false;
        holdTimer = 0f;
        gameObject.SetActive(false);
    }

    public IEnumerator HideWithShrink()
    {
        StopHoldSound();
        isHiding = true;
        holdTimer = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = originalScale * hideGrowMultiplier;
        Color ownStartColor = ownRenderer != null ? ownRenderer.color : waitingColor;
        Color progressStartColor = progressRenderer != null ? progressRenderer.color : waitingColor;
        float duration = Mathf.Max(0.01f, hideGrowFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            SetRendererAlpha(1f - smoothT, ownStartColor, progressStartColor);
            yield return null;
        }

        gameObject.SetActive(false);
        transform.localScale = originalScale;
        SetRendererAlpha(1f);
        isHiding = false;
    }

    private void UpdateVisual(bool isHolding)
    {
        float progress = holdSeconds > 0f ? Mathf.Clamp01(holdTimer / holdSeconds) : 1f;
        Vector3 targetScale = originalScale * (isHolding ? hoverScaleMultiplier : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleLerpSpeed * Time.deltaTime);

        if (ownRenderer != null)
            ownRenderer.color = isHolding ? Color.Lerp(waitingColor, pressedColor, progress) : waitingColor;

        if (progressRenderer != null)
        {
            progressRenderer.transform.localScale = new Vector3(progress, progress, 1f);
        }

        if (isHolding)
            soundManager?.PlayStartButtonHold(progress);
        else
            StopHoldSound();
    }

    private void StopHoldSound()
    {
        soundManager?.StopStartButtonHold();
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

    private void ApplyZPosition()
    {
        Vector3 position = transform.position;
        position.z = buttonZ;
        transform.position = position;
    }

    private void ApplySortingOrder()
    {
        if (ownRenderer == null)
            ownRenderer = GetComponent<SpriteRenderer>();

        if (ownRenderer != null)
            ownRenderer.sortingOrder = buttonSortingOrder;

        if (progressRenderer != null)
            progressRenderer.sortingOrder = buttonSortingOrder + 1;
    }

    private void SetRendererAlpha(float alpha)
    {
        SetRendererAlpha(alpha, waitingColor, waitingColor);
    }

    private void SetRendererAlpha(float alpha, Color ownBaseColor, Color progressBaseColor)
    {
        if (ownRenderer != null)
        {
            Color color = ownBaseColor;
            color.a = alpha;
            ownRenderer.color = color;
        }

        if (progressRenderer != null)
        {
            Color color = progressBaseColor;
            color.a = alpha;
            progressRenderer.color = color;
        }
    }

    private void OnValidate()
    {
        ApplyZPosition();
        ApplySortingOrder();
    }
}
