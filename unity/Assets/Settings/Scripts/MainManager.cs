using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MainManager : MonoBehaviour
{
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
    [SerializeField] private float rankingShowDelay = 1f;
    [SerializeField] private float rankingSlideDuration = 0.6f;
    [SerializeField] private Vector2 rankingSlideFromOffset = new Vector2(-900f, 0f);
    [SerializeField] private float clearCleanupDelay = 5f;
    [SerializeField] private Vector3 playerCenterPosition = new Vector3(0f, 0f, -2f);

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
    [SerializeField] private Color countdownColor = Color.white;

    private Coroutine clearCleanupCoroutine;
    private string clearTextOriginalText = "Game Clear";
    private bool previousShowRankingDebugAlways;
    private bool isStartingGame;
    private Canvas transitionCanvas;
    private CircleWipeOverlay transitionOverlay;
    private Coroutine rankingShowCoroutine;
    private Vector3 rankingRootVisibleLocalPosition;
    private bool hasRankingRootVisibleLocalPosition;
    private readonly List<RankingSpriteSlideState> rankingSpriteSlideStates = new List<RankingSpriteSlideState>();

    private class RankingSpriteSlideState
    {
        public Transform transform;
        public Vector3 startWorldPosition;
        public Vector3 targetWorldPosition;
    }

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

        Vector2 openCenter = GetScreenPosition(player != null ? player.transform.position : playerCenterPosition);
        yield return RunCircleWipe(openCenter, 0f, GetFullScreenHoleRadius(), transitionOpenDuration);
        HideTransitionOverlay();

        if (shadowManager != null)
            shadowManager.enabled = true;

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

        // クリアテキスト表示
        if (clearText != null)
            clearText.SetActive(true);

        float clearTimeSeconds = timeManager != null ? timeManager.ElapsedTime : 0f;
        TMP_Text clearTextLabel = clearText != null ? clearText.GetComponent<TMP_Text>() : null;
        if (clearTextLabel != null)
            clearTextLabel.text = clearTextOriginalText;

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
        ApplyRankingTextStyle();
        rankingText.text = text;
        CacheRankingRootVisiblePosition(slideTarget);
        CaptureRankingSpriteSlideStates(slideTarget);

        slideTarget.localPosition = rankingRootVisibleLocalPosition + (Vector3)rankingSlideFromOffset;
        slideTarget.gameObject.SetActive(true);
        ApplyRankingSpriteSlidePosition(0f);
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
            ApplyRankingSpriteSlidePosition(smoothT);

            yield return null;
        }

        slideTarget.localPosition = targetPosition;
        ApplyRankingSpriteSlidePosition(1f);
    }

    private void CacheRankingRootVisiblePosition(Transform slideTarget)
    {
        if (hasRankingRootVisibleLocalPosition)
            return;

        slideTarget.gameObject.SetActive(true);
        rankingRootVisibleLocalPosition = slideTarget.localPosition;
        hasRankingRootVisibleLocalPosition = true;
    }

    private void CaptureRankingSpriteSlideStates(Transform slideTarget)
    {
        rankingSpriteSlideStates.Clear();

        SpriteRenderer[] spriteRenderers = slideTarget.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
                continue;

            spriteRenderer.gameObject.SetActive(true);
            spriteRenderer.enabled = true;

            Vector3 targetWorldPosition = spriteRenderer.transform.position;
            rankingSpriteSlideStates.Add(new RankingSpriteSlideState
            {
                transform = spriteRenderer.transform,
                startWorldPosition = GetWorldPositionWithScreenOffset(targetWorldPosition, rankingSlideFromOffset),
                targetWorldPosition = targetWorldPosition
            });
        }
    }

    private void ApplyRankingSpriteSlidePosition(float t)
    {
        foreach (RankingSpriteSlideState state in rankingSpriteSlideStates)
        {
            if (state.transform == null)
                continue;

            state.transform.position = Vector3.Lerp(state.startWorldPosition, state.targetWorldPosition, t);
        }
    }

    private Vector3 GetWorldPositionWithScreenOffset(Vector3 worldPosition, Vector2 screenOffset)
    {
        Camera cameraToUse = transitionCamera != null ? transitionCamera : Camera.main;
        if (cameraToUse == null)
            return worldPosition + (Vector3)screenOffset;

        Vector3 screenPosition = cameraToUse.WorldToScreenPoint(worldPosition);
        screenPosition.x += screenOffset.x;
        screenPosition.y += screenOffset.y;
        return cameraToUse.ScreenToWorldPoint(screenPosition);
    }

    private void RefreshRankingDebugVisibility()
    {
        if (rankingManager != null && rankingText != null)
        {
            ApplyRankingTextStyle();
            rankingText.text = rankingManager.BuildCurrentTopText();
        }

        if (rankingText != null)
        {
            if (rankingRoot == null)
                rankingRoot = rankingText.GetComponent<RectTransform>();

            Transform visibilityTarget = rankingRoot != null ? rankingRoot : rankingText.transform;
            visibilityTarget.gameObject.SetActive(showRankingDebugAlways);
        }
    }

    private void ApplyRankingTextStyle()
    {
        if (rankingText != null)
            rankingText.alignment = rankingTextAlignment;
    }
}
