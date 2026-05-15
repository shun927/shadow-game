using UnityEngine;
using TMPro;
using System.Collections;

public class MainManager : MonoBehaviour
{
    private const string RankingTextBackgroundObjectName = "Ranking Text Background";

    [SerializeField] private GameObject clearText;
    [SerializeField] private Player player;
    [SerializeField] private ShadowManager shadowManager;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private PointManager pointManager;
    [SerializeField] private StartButton startButton;
    [SerializeField] private SoundManager soundManager;
    [SerializeField] private RankingManager rankingManager;
    [SerializeField] private TMP_Text rankingText;
    [SerializeField] private RectTransform rankingRoot;
    [SerializeField] private bool showRankingDebugAlways;
    [SerializeField] private TextAlignmentOptions rankingTextAlignment = TextAlignmentOptions.Top;
    [SerializeField] private Vector3 rankingTextOffset = new Vector3(0f, -1.2f, 0f);
    [SerializeField] private bool showRankingTextBackground = true;
    [SerializeField] private Color rankingTextBackgroundColor = new Color(0f, 0f, 0f, 0.76f);
    [SerializeField] private Vector2 rankingTextBackgroundPadding = new Vector2(24f, 14f);
    [SerializeField] private Vector2 rankingTextBackgroundBlockMargin = new Vector2(40f, 24f);
    [SerializeField] private Vector2 rankingTextBackgroundMinSize = new Vector2(0f, 0f);
    [SerializeField] private Vector2 rankingTextBackgroundOffset;
    [SerializeField] private float rankingTextBackgroundCornerRadius = 8f;
    [SerializeField] private int rankingTextBackgroundCornerSegments = 8;
    [SerializeField] private Vector2 rankingLatestHighlightPadding = new Vector2(16f, 6f);
    [SerializeField] private Vector2 rankingLatestHighlightMinSize = new Vector2(0f, 0f);
    [SerializeField] private float rankingLatestHighlightCornerRadius = 8f;
    [SerializeField] private int rankingLatestHighlightCornerSegments = 8;
    [SerializeField] private float rankingShowDelay = 1f;
    [SerializeField] private float rankingSlideDuration = 0.6f;
    [SerializeField] private Vector2 rankingSlideFromOffset = new Vector2(-900f, 0f);
    [SerializeField] private float clearCleanupDelay = 5f;
    [SerializeField] private Vector3 playerCenterPosition = new Vector3(0f, 0f, -2f);
    [SerializeField] private float clearTextGradientDuration = 0.6f;
    [SerializeField] private Color clearTextGradientTopColor = Color.white;
    [SerializeField] private Color clearTextGradientBottomColor = Color.white;

    [Header("Start Transition")]
    [SerializeField] private Camera transitionCamera;
    [SerializeField] private float transitionCloseDuration = 0.8f;
    [SerializeField] private float transitionBlackHoldDuration = 1f;
    [SerializeField] private float transitionOpenDuration = 0.8f;
    [SerializeField] private int transitionCircleSegments = 96;
    [SerializeField] private Color transitionColor = Color.black;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private int countdownSeconds = 3;
    [SerializeField] private float countdownFontSize = 200f;
    [SerializeField] private Color countdownColor = new Color(0f, 0f, 0f, 0.55f);

    private Coroutine clearCleanupCoroutine;
    private string clearTextOriginalText = "Game Clear";
    private bool previousShowRankingDebugAlways;
    private bool isStartingGame;
    private Canvas transitionCanvas;
    private CircleWipeOverlay transitionOverlay;
    private Coroutine rankingShowCoroutine;
    private Coroutine clearTextGradientCoroutine;
    private RectTransform rankingTextBackground;
    private RoundedRectangleGraphic rankingTextBackgroundGraphic;
    private RectTransform rankingLatestHighlightBackground;
    private TMP_Text rankingLatestHighlightText;
    private Vector3 rankingRootVisibleLocalPosition;
    private bool hasRankingRootVisibleLocalPosition;
    void Awake()
    {
        if (player != null)
            playerCenterPosition = player.transform.position;

        if (rankingManager == null)
            rankingManager = FindFirstObjectByType<RankingManager>();

        if (rankingManager == null)
            rankingManager = gameObject.AddComponent<RankingManager>();

        if (soundManager == null)
            soundManager = SoundManager.GetOrCreate();

        if (transitionCamera == null)
            transitionCamera = Camera.main;

        CacheClearText();
        ValidateRankingText();
        EnsureTransitionUi();
    }

    void Start()
    {
        ResetToInitialState();
        previousShowRankingDebugAlways = showRankingDebugAlways;
    }

    void Update()
    {
        if (showRankingDebugAlways)
            RefreshRankingDebugPreview();

        if (previousShowRankingDebugAlways == showRankingDebugAlways)
            return;

        previousShowRankingDebugAlways = showRankingDebugAlways;
        RefreshRankingDebugVisibility();
    }

    public void StartGame()
    {
        if (isStartingGame)
            return;

        StartCoroutine(StartGameSequence());
    }

    private IEnumerator StartGameSequence()
    {
        isStartingGame = true;
        if (shadowManager != null)
            shadowManager.CancelRespawnEffects();

        if (clearCleanupCoroutine != null)
        {
            StopCoroutine(clearCleanupCoroutine);
            clearCleanupCoroutine = null;
        }

        if (clearText != null)
            clearText.SetActive(false);

        if (player != null)
            player.enabled = true;

        if (startButton != null)
            yield return startButton.HideWithShrink();

        if (shadowManager != null)
        {
            shadowManager.SetOutsideShadowPenaltyEnabled(true);
            shadowManager.enabled = true;
        }

        Vector2 closeCenter = GetScreenPosition(startButton != null ? startButton.transform.position : Vector3.zero);
        yield return RunCircleWipe(closeCenter, GetFullScreenHoleRadius(), 0f, transitionCloseDuration);
        yield return WaitForFullBlackTransition();

        if (player != null)
        {
            player.transform.position = playerCenterPosition;
            player.gameObject.SetActive(true);
            player.SetVisible(true);
            player.ResetChildrenAlpha();
            player.enabled = false;
        }

        if (shadowManager != null)
        {
            shadowManager.ResetForNewGame();
            shadowManager.SetOutsideShadowPenaltyEnabled(false);
            shadowManager.enabled = false;
        }

        if (player != null)
            player.enabled = false;

        Vector2 openCenter = GetScreenPosition(player != null ? player.transform.position : playerCenterPosition);
        yield return RunCircleWipe(openCenter, 0f, GetFullScreenHoleRadius(), transitionOpenDuration);
        HideTransitionOverlay();

        if (shadowManager != null)
            shadowManager.enabled = true;

        if (player != null)
            player.enabled = false;

        yield return RunCountdown();

        BeginGameplay();
        isStartingGame = false;
    }

    private void BeginGameplay()
    {
        if (soundManager != null)
            soundManager.PlayGameplayBgm();

        if (player != null)
            player.enabled = true;

        if (shadowManager != null)
        {
            shadowManager.SetOutsideShadowPenaltyEnabled(true);
            shadowManager.ResetForNewGame();
            shadowManager.enabled = true;
        }

        if (timeManager != null)
            timeManager.ResetTimer();

        if (pointManager != null)
            pointManager.ResetGame(true);
    }

    public void OnGameClear()
    {
        Debug.Log("[MainManager] OnGameClear called!");

        if (soundManager != null)
            soundManager.PlayResultBgm();

        if (clearCleanupCoroutine != null)
            StopCoroutine(clearCleanupCoroutine);

        float clearTimeSeconds = timeManager != null ? timeManager.ElapsedTime : 0f;
        TMP_Text clearTextLabel = clearText != null ? clearText.GetComponent<TMP_Text>() : null;
        if (clearTextLabel != null)
        {
            clearTextLabel.text = clearTextOriginalText;
            if (clearTextGradientCoroutine != null)
                StopCoroutine(clearTextGradientCoroutine);
            clearTextGradientCoroutine = StartCoroutine(ShowClearTextWithGradient(clearTextLabel));
        }
        else if (clearText != null)
        {
            clearText.SetActive(true);
        }

        if (rankingManager != null)
        {
            string rankingTextContent = rankingManager.RecordClearAndBuildTopText(clearTimeSeconds);
            if (rankingShowCoroutine != null)
                StopCoroutine(rankingShowCoroutine);
            rankingShowCoroutine = StartCoroutine(ShowRankingAfterDelay(rankingTextContent));
        }

        if (player != null)
        {
            player.enabled = true;
            player.SetVisible(true);
            player.ResetChildrenAlpha();
        }

        // タイマー停止
        if (timeManager != null)
            timeManager.StopTimer();

        if (shadowManager != null)
            shadowManager.enabled = false;

        if (startButton != null)
            startButton.Hide();

        clearCleanupCoroutine = StartCoroutine(CleanupAfterClear());
    }

    private IEnumerator CleanupAfterClear()
    {
        yield return new WaitForSeconds(clearCleanupDelay);

        yield return RunResultResetTransition();

        clearCleanupCoroutine = null;
    }

    private IEnumerator RunResultResetTransition()
    {
        Vector3 closeWorldPosition = player != null ? player.transform.position : playerCenterPosition;
        Vector2 closeCenter = GetScreenPosition(closeWorldPosition);
        yield return RunCircleWipe(closeCenter, GetFullScreenHoleRadius(), 0f, transitionCloseDuration);
        yield return WaitForFullBlackTransition();

        ResetToInitialState();

        Vector2 openCenter = GetScreenPosition(player != null ? player.transform.position : playerCenterPosition);
        yield return RunCircleWipe(openCenter, 0f, GetFullScreenHoleRadius(), transitionOpenDuration);
        HideTransitionOverlay();
    }

    private void ResetToInitialState()
    {
        if (clearText != null)
            clearText.SetActive(false);

        if (clearTextGradientCoroutine != null)
        {
            StopCoroutine(clearTextGradientCoroutine);
            clearTextGradientCoroutine = null;
        }

        if (rankingShowCoroutine != null)
        {
            StopCoroutine(rankingShowCoroutine);
            rankingShowCoroutine = null;
        }

        if (player != null)
        {
            player.gameObject.SetActive(true);
            player.transform.position = playerCenterPosition;
            player.ResetChildrenAlpha();
            player.SetVisible(true);
            player.enabled = true;
        }

        if (shadowManager != null)
        {
            shadowManager.SetOutsideShadowPenaltyEnabled(true);
            shadowManager.ResetForNewGame();
            shadowManager.enabled = true;
        }

        if (timeManager != null)
            timeManager.ResetTimerStopped();

        if (pointManager != null)
            pointManager.ResetGame(false);

        if (startButton != null)
            startButton.Show();

        if (soundManager != null)
            soundManager.StopAllManagedSounds();

        RefreshRankingDebugVisibility();
    }

    private IEnumerator RunCircleWipe(Vector2 screenCenter, float fromRadius, float toRadius, float duration)
    {
        EnsureTransitionUi();
        if (transitionOverlay == null)
            yield break;

        transitionOverlay.gameObject.SetActive(true);
        transitionOverlay.color = new Color(0f, 0f, 0f, transitionColor.a);
        transitionOverlay.SetSegments(transitionCircleSegments);

        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            transitionOverlay.SetHole(screenCenter, Mathf.Lerp(fromRadius, toRadius, smoothT));
            yield return null;
        }

        transitionOverlay.SetHole(screenCenter, toRadius);
    }

    private IEnumerator WaitForFullBlackTransition()
    {
        float holdDuration = Mathf.Max(0f, transitionBlackHoldDuration);
        if (holdDuration <= 0f)
            yield break;

        yield return new WaitForSeconds(holdDuration);
    }

    private IEnumerator RunCountdown()
    {
        EnsureTransitionUi();
        if (countdownText == null)
            yield break;

        if (player != null)
            player.enabled = false;

        countdownText.gameObject.SetActive(true);
        countdownText.fontSize = countdownFontSize;
        countdownText.color = countdownColor;
        countdownText.alignment = TextAlignmentOptions.Center;

        int start = Mathf.Max(1, countdownSeconds);
        for (int number = start; number >= 1; number--)
        {
            countdownText.text = number.ToString();
            if (soundManager != null)
                soundManager.PlayCountdownTick();
            yield return new WaitForSeconds(1f);
        }

        countdownText.gameObject.SetActive(false);
    }

    private void EnsureTransitionUi()
    {
        if (transitionCanvas == null)
        {
            GameObject canvasObject = new GameObject("Start Transition Canvas");
            transitionCanvas = canvasObject.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 5000;
        }

        if (transitionOverlay == null)
        {
            GameObject overlayObject = new GameObject("Circle Wipe Overlay");
            overlayObject.transform.SetParent(transitionCanvas.transform, false);
            RectTransform overlayRect = overlayObject.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            transitionOverlay = overlayObject.AddComponent<CircleWipeOverlay>();
            transitionOverlay.raycastTarget = false;
            transitionOverlay.gameObject.SetActive(false);
        }

        if (countdownText == null)
        {
            GameObject textObject = new GameObject("Countdown Text");
            textObject.transform.SetParent(transitionCanvas.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            countdownText = textObject.AddComponent<TextMeshProUGUI>();
            countdownText.raycastTarget = false;
            countdownText.gameObject.SetActive(false);
        }
    }

    private void HideTransitionOverlay()
    {
        if (transitionOverlay != null)
            transitionOverlay.gameObject.SetActive(false);
    }

    private Vector2 GetScreenPosition(Vector3 worldPosition)
    {
        Camera cameraToUse = transitionCamera != null ? transitionCamera : Camera.main;
        if (cameraToUse == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        return cameraToUse.WorldToScreenPoint(worldPosition);
    }

    private float GetFullScreenHoleRadius()
    {
        return new Vector2(Screen.width, Screen.height).magnitude;
    }

    private void CacheClearText()
    {
        TMP_Text clearTextLabel = clearText != null ? clearText.GetComponent<TMP_Text>() : null;
        if (clearTextLabel != null)
            clearTextOriginalText = clearTextLabel.text;
    }

    private void ValidateRankingText()
    {
        if (rankingText != null)
            return;

        Debug.LogWarning("[MainManager] rankingText is not assigned. Ranking will not be shown.");
    }

    private void SetRankingText(string text, bool visible)
    {
        if (rankingText == null)
            return;

        if (rankingRoot == null)
            rankingRoot = rankingText.GetComponent<RectTransform>();

        ApplyRankingTextStyle();
        rankingText.text = text;
        rankingRoot.gameObject.SetActive(visible || showRankingDebugAlways);
        UpdateRankingTextBackground(visible || showRankingDebugAlways);
        ApplyRankingChildSpriteVisibility();
        UpdateRankingLatestHighlight();
    }

    private IEnumerator ShowRankingAfterDelay(string text)
    {
        PrepareRankingSlideStart(text);
        yield return new WaitForSeconds(Mathf.Max(0f, rankingShowDelay));
        yield return SlideInRanking(text);
        rankingShowCoroutine = null;
    }

    private void PrepareRankingSlideStart(string text)
    {
        if (rankingText == null)
            return;

        if (rankingRoot == null)
            rankingRoot = rankingText.GetComponent<RectTransform>();

        Transform slideTarget = rankingRoot != null ? rankingRoot : rankingText.transform;
        CacheRankingRootVisiblePosition(slideTarget);
        ApplyRankingTextStyle();
        rankingText.text = text;
        UpdateRankingTextBackground(true);
        ApplyRankingChildSpriteVisibility();
        UpdateRankingLatestHighlight();

        slideTarget.localPosition = rankingRootVisibleLocalPosition + (Vector3)rankingSlideFromOffset;
        slideTarget.gameObject.SetActive(true);
        SyncRankingTextBackgroundTransform();
    }

    private IEnumerator SlideInRanking(string text)
    {
        if (rankingText == null)
            yield break;

        if (rankingRoot == null)
            rankingRoot = rankingText.GetComponent<RectTransform>();

        Transform slideTarget = rankingRoot != null ? rankingRoot : rankingText.transform;
        ApplyRankingTextStyle();
        rankingText.text = text;
        UpdateRankingTextBackground(true);
        ApplyRankingChildSpriteVisibility();
        UpdateRankingLatestHighlight();
        CacheRankingRootVisiblePosition(slideTarget);

        Vector3 startPosition = slideTarget.localPosition;
        Vector3 targetPosition = rankingRootVisibleLocalPosition;

        slideTarget.gameObject.SetActive(true);

        float duration = Mathf.Max(0.01f, rankingSlideDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            slideTarget.localPosition = Vector3.Lerp(startPosition, targetPosition, smoothT);
            SyncRankingTextBackgroundTransform();

            yield return null;
        }

        slideTarget.localPosition = targetPosition;
        SyncRankingTextBackgroundTransform();
    }

    private void CacheRankingRootVisiblePosition(Transform slideTarget)
    {
        if (hasRankingRootVisibleLocalPosition)
            return;

        slideTarget.gameObject.SetActive(true);
        rankingRootVisibleLocalPosition = slideTarget.localPosition;
        hasRankingRootVisibleLocalPosition = true;
    }

    private void RefreshRankingDebugVisibility()
    {
        if (rankingManager != null && rankingText != null)
        {
            ApplyRankingTextStyle();
            rankingText.text = rankingManager.BuildCurrentTopText();
            UpdateRankingLatestHighlight();
        }

        if (rankingText != null)
        {
            if (rankingRoot == null)
                rankingRoot = rankingText.GetComponent<RectTransform>();

            Transform visibilityTarget = rankingRoot != null ? rankingRoot : rankingText.transform;
            visibilityTarget.gameObject.SetActive(showRankingDebugAlways);
            UpdateRankingTextBackground(showRankingDebugAlways);
            ApplyRankingChildSpriteVisibility();
            UpdateRankingLatestHighlight();
        }
    }

    private void RefreshRankingDebugPreview()
    {
        if (rankingText == null)
            return;

        if (rankingRoot == null)
            rankingRoot = rankingText.GetComponent<RectTransform>();

        Transform visibilityTarget = rankingRoot != null ? rankingRoot : rankingText.transform;
        if (!visibilityTarget.gameObject.activeSelf)
            visibilityTarget.gameObject.SetActive(true);

        ApplyRankingTextStyle();
        UpdateRankingTextBackground(true);
        ApplyRankingChildSpriteVisibility();
        UpdateRankingLatestHighlight();
    }

    private void ApplyRankingTextStyle()
    {
        if (rankingText != null)
            rankingText.alignment = rankingTextAlignment;
    }

    private void ApplyRankingChildSpriteVisibility()
    {
        if (rankingRoot == null)
            return;

        SpriteRenderer[] spriteRenderers = rankingRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
                continue;

            spriteRenderer.enabled = !showRankingTextBackground;
        }
    }

    private void UpdateRankingTextBackground(bool visible)
    {
        EnsureRankingTextBackgroundObject();
        if (rankingTextBackground == null || rankingTextBackgroundGraphic == null)
            return;

        rankingTextBackground.gameObject.SetActive(showRankingTextBackground && visible);
        if (!showRankingTextBackground || !visible)
            return;

        Bounds textBounds = GetRankingTextBoundsInBackgroundParent();
        SyncRankingTextBackgroundTransform(textBounds.center);

        Vector2 size = textBounds.size;
        if (size.x <= 1f || size.y <= 1f)
            size = new Vector2(rankingText.preferredWidth, rankingText.preferredHeight);

        size += (rankingTextBackgroundPadding + rankingTextBackgroundBlockMargin) * 2f;
        size.x = Mathf.Max(size.x, rankingTextBackgroundMinSize.x);
        size.y = Mathf.Max(size.y, rankingTextBackgroundMinSize.y);

        rankingTextBackground.sizeDelta = size;
        rankingTextBackgroundGraphic.color = rankingTextBackgroundColor;
        rankingTextBackgroundGraphic.CornerRadius = rankingTextBackgroundCornerRadius;
        rankingTextBackgroundGraphic.CornerSegments = rankingTextBackgroundCornerSegments;
    }

    private void EnsureRankingTextBackgroundObject()
    {
        if (rankingTextBackground != null && rankingTextBackgroundGraphic != null)
            return;

        if (rankingRoot == null && rankingText != null)
            rankingRoot = rankingText.GetComponent<RectTransform>();

        RectTransform backgroundParent = GetRankingTextBackgroundParent();
        if (rankingRoot == null || backgroundParent == null)
            return;

        Transform existingBackground = backgroundParent.Find(RankingTextBackgroundObjectName);
        GameObject backgroundObject = existingBackground != null
            ? existingBackground.gameObject
            : new GameObject(RankingTextBackgroundObjectName);

        backgroundObject.layer = rankingRoot.gameObject.layer;
        backgroundObject.transform.SetParent(backgroundParent, false);
        rankingTextBackground = backgroundObject.GetComponent<RectTransform>();
        if (rankingTextBackground == null)
            rankingTextBackground = backgroundObject.AddComponent<RectTransform>();

        rankingTextBackgroundGraphic = backgroundObject.GetComponent<RoundedRectangleGraphic>();
        if (rankingTextBackgroundGraphic == null)
            rankingTextBackgroundGraphic = backgroundObject.AddComponent<RoundedRectangleGraphic>();

        rankingTextBackgroundGraphic.raycastTarget = false;
        rankingTextBackground.gameObject.SetActive(false);
        SyncRankingTextBackgroundTransform();
    }

    private RectTransform GetRankingTextBackgroundParent()
    {
        if (rankingRoot != null && rankingRoot.parent is RectTransform parent)
            return parent;

        if (rankingText != null)
        {
            RectTransform textRect = rankingText.GetComponent<RectTransform>();
            if (textRect != null)
                return textRect.parent as RectTransform;
        }

        return null;
    }

    private Bounds GetRankingTextBoundsInBackgroundParent()
    {
        if (rankingText == null)
            return new Bounds(Vector3.zero, Vector3.zero);

        rankingText.ForceMeshUpdate(true, true);
        Bounds textBounds = rankingText.textBounds;
        RectTransform backgroundParent = GetRankingTextBackgroundParent();
        if (backgroundParent == null)
            return textBounds;

        Vector3 min = textBounds.min;
        Vector3 max = textBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, 0f),
            new Vector3(min.x, max.y, 0f),
            new Vector3(max.x, max.y, 0f),
            new Vector3(max.x, min.y, 0f)
        };

        Bounds parentBounds = new Bounds(
            backgroundParent.InverseTransformPoint(rankingText.transform.TransformPoint(corners[0])),
            Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
            parentBounds.Encapsulate(backgroundParent.InverseTransformPoint(rankingText.transform.TransformPoint(corners[i])));

        return parentBounds;
    }

    private void SyncRankingTextBackgroundTransform()
    {
        SyncRankingTextBackgroundTransform(GetRankingTextBoundsInBackgroundParent().center);
    }

    private void SyncRankingTextBackgroundTransform(Vector3 textParentCenter)
    {
        if (rankingTextBackground == null || rankingRoot == null)
            return;

        rankingTextBackground.anchorMin = new Vector2(0.5f, 0.5f);
        rankingTextBackground.anchorMax = new Vector2(0.5f, 0.5f);
        rankingTextBackground.pivot = new Vector2(0.5f, 0.5f);
        rankingTextBackground.localPosition = textParentCenter + (Vector3)rankingTextBackgroundOffset;
        rankingTextBackground.localRotation = Quaternion.identity;
        rankingTextBackground.localScale = Vector3.one;

        int targetSiblingIndex = rankingRoot.GetSiblingIndex();
        if (rankingTextBackground.GetSiblingIndex() < targetSiblingIndex)
            targetSiblingIndex--;

        rankingTextBackground.SetSiblingIndex(Mathf.Max(0, targetSiblingIndex));
    }

    private void UpdateRankingLatestHighlight()
    {
        if (rankingText == null || rankingManager == null || rankingManager.LatestHighlightedLineIndex < 0)
        {
            SetRankingLatestHighlightVisible(false);
            return;
        }

        EnsureRankingLatestHighlightObjects();
        if (rankingLatestHighlightBackground == null || rankingLatestHighlightText == null)
            return;

        rankingText.ForceMeshUpdate(true, true);
        TMP_TextInfo textInfo = rankingText.textInfo;
        int lineIndex = rankingManager.LatestHighlightedLineIndex;
        if (lineIndex < 0 || lineIndex >= textInfo.lineCount)
        {
            SetRankingLatestHighlightVisible(false);
            return;
        }

        TMP_LineInfo lineInfo = textInfo.lineInfo[lineIndex];
        float width = Mathf.Max(1f, lineInfo.lineExtents.max.x - lineInfo.lineExtents.min.x);
        float height = Mathf.Max(1f, lineInfo.ascender - lineInfo.descender);
        Vector2 center = new Vector2(
            (lineInfo.lineExtents.min.x + lineInfo.lineExtents.max.x) * 0.5f,
            (lineInfo.ascender + lineInfo.descender) * 0.5f);
        Vector2 size = new Vector2(
            width + rankingLatestHighlightPadding.x * 2f,
            height + rankingLatestHighlightPadding.y * 2f);
        size.x = Mathf.Max(size.x, rankingLatestHighlightMinSize.x);
        size.y = Mathf.Max(size.y, rankingLatestHighlightMinSize.y);

        rankingLatestHighlightBackground.anchoredPosition = center;
        rankingLatestHighlightBackground.sizeDelta = size;
        RoundedRectangleGraphic backgroundGraphic = rankingLatestHighlightBackground.GetComponent<RoundedRectangleGraphic>();
        if (backgroundGraphic != null)
        {
            backgroundGraphic.color = rankingManager.LatestEntryMarkColor;
            backgroundGraphic.CornerRadius = rankingLatestHighlightCornerRadius;
            backgroundGraphic.CornerSegments = rankingLatestHighlightCornerSegments;
        }

        RectTransform highlightTextRect = rankingLatestHighlightText.rectTransform;
        highlightTextRect.anchoredPosition = center;
        highlightTextRect.sizeDelta = size;
        rankingLatestHighlightText.text = rankingManager.LatestHighlightedLineText;
        rankingLatestHighlightText.color = rankingManager.LatestEntryColor;
        rankingLatestHighlightText.font = rankingText.font;
        rankingLatestHighlightText.fontSharedMaterial = rankingText.fontSharedMaterial;
        rankingLatestHighlightText.fontSize = rankingText.fontSize;
        rankingLatestHighlightText.fontStyle = rankingText.fontStyle;
        rankingLatestHighlightText.alignment = TextAlignmentOptions.Center;
        rankingLatestHighlightText.raycastTarget = false;

        SetRankingLatestHighlightVisible(true);
    }

    private void EnsureRankingLatestHighlightObjects()
    {
        if (rankingText == null)
            return;

        RectTransform parent = rankingRoot != null ? rankingRoot : rankingText.GetComponent<RectTransform>();
        if (parent == null)
            return;

        if (rankingLatestHighlightBackground == null)
        {
            GameObject backgroundObject = new GameObject("Latest Entry Highlight");
            backgroundObject.transform.SetParent(parent, false);
            rankingLatestHighlightBackground = backgroundObject.AddComponent<RectTransform>();
            rankingLatestHighlightBackground.anchorMin = new Vector2(0.5f, 0.5f);
            rankingLatestHighlightBackground.anchorMax = new Vector2(0.5f, 0.5f);
            rankingLatestHighlightBackground.pivot = new Vector2(0.5f, 0.5f);
            RoundedRectangleGraphic roundedRectangle = backgroundObject.AddComponent<RoundedRectangleGraphic>();
            roundedRectangle.raycastTarget = false;
        }

        if (rankingLatestHighlightText == null)
        {
            GameObject textObject = new GameObject("Latest Entry Text");
            textObject.transform.SetParent(parent, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            rankingLatestHighlightText = textObject.AddComponent<TextMeshProUGUI>();
            rankingLatestHighlightText.raycastTarget = false;
        }

        rankingLatestHighlightBackground.SetAsLastSibling();
        rankingLatestHighlightText.rectTransform.SetAsLastSibling();
    }

    private void SetRankingLatestHighlightVisible(bool visible)
    {
        if (rankingLatestHighlightBackground != null)
            rankingLatestHighlightBackground.gameObject.SetActive(visible);

        if (rankingLatestHighlightText != null)
            rankingLatestHighlightText.gameObject.SetActive(visible);
    }

    private IEnumerator ShowClearTextWithGradient(TMP_Text text)
    {
        if (clearText != null)
            clearText.SetActive(true);

        text.enableVertexGradient = true;

        float duration = Mathf.Max(0.01f, clearTextGradientDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            ApplyClearTextGradient(text, smoothT);
            yield return null;
        }

        ApplyClearTextGradient(text, 1f);
        clearTextGradientCoroutine = null;
    }

    private void ApplyClearTextGradient(TMP_Text text, float alpha)
    {
        Color top = clearTextGradientTopColor;
        Color bottom = clearTextGradientBottomColor;
        float bottomAlpha = Mathf.Clamp01((alpha - 0.35f) / 0.65f);
        top.a *= alpha;
        bottom.a *= bottomAlpha;
        text.colorGradient = new VertexGradient(top, top, bottom, bottom);
    }
}
