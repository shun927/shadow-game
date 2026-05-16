using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointManager : MonoBehaviour
{
    private enum PointMovementMode
    {
        Fixed,
        Orbit,
        Flee,
    }

    [SerializeField] private GameObject pointPrefab;

    [Header("Count Sprites")]
    [SerializeField] private GameObject pointCount1;
    [SerializeField] private GameObject pointCount2;
    [SerializeField] private GameObject pointCount3;

    [Header("Spawn Positions")]
    [SerializeField] private Vector2[] spawnPositions1;
    [SerializeField] private Vector2[] spawnPositions2;
    [SerializeField] private Vector2[] spawnPositions3;

    [Header("Movement Per Point")]
    [SerializeField] private PointMovementMode point1Movement = PointMovementMode.Fixed;
    [SerializeField] private PointMovementMode point2Movement = PointMovementMode.Orbit;
    [SerializeField] private PointMovementMode point3Movement = PointMovementMode.Flee;

    [Header("Collection")]
    [SerializeField] private float collectDistance = 0.5f;
    [SerializeField] private Transform player;
    [SerializeField] private float pointZ = 0f;
    [SerializeField] private int pointSortingOrder = 10;
    [SerializeField] private ShadowManager shadowManager;
    [SerializeField] private MainManager mainManager;

    [Header("Collect Animation")]
    [SerializeField] private float collectMoveDuration = 0.45f;

    [Header("Spawn Animation")]
    [SerializeField] private float spawnGrowDuration = 0.35f;
    [SerializeField] private float spawnStartScale = 0.1f;

    [Header("Point Marker")]
    [SerializeField] private bool showPointMarker = true;
    [SerializeField] private float pointMarkerMinSize = 0.7f;
    [SerializeField] private float pointMarkerMaxSize = 1.15f;
    [SerializeField] private float pointMarkerPulseDuration = 0.8f;
    [SerializeField] private float pointMarkerRotationZ = 45f;
    [SerializeField] private float pointMarkerZOffset = -0.02f;
    [SerializeField] private int pointMarkerSortingOrderOffset = 1;
    [SerializeField] private float pointMarkerInvertAmount = 1f;

    [Header("Collect Square Effect")]
    [SerializeField] private bool showCollectSquareEffect = true;
    [SerializeField] private float collectSquareEffectDuration = 0.9f;
    [SerializeField] private float collectSquareStartSize = 0.2f;
    [SerializeField] private float collectSquareEndSize = 3f;
    [SerializeField] private bool collectSquareFillScreen = true;
    [SerializeField] private float collectSquareRotationZ = 45f;
    [SerializeField] private float collectSquareScreenCoverPadding = 1.2f;
    [SerializeField] private float collectSquareZ = -1f;
    [SerializeField] private int collectSquareSortingOrder = 20;
    [SerializeField] private float collectSquareInvertAmount = 1f;
    [SerializeField] private float collectSquareRestoreDelay = 0.18f;
    [SerializeField] private float collectSquareRestoreDuration = 0.45f;

    [Header("Point 2 - Orbit")]
    [SerializeField] private float orbitRadius = 1.5f;
    [SerializeField] private float orbitSpeed = 120f;

    [Header("Point 3 - Flee")]
    [SerializeField] private float fleeSpeed = 3f;
    [SerializeField] private float playerAvoidWeight = 0.3f;
    [SerializeField] private Vector2 boundsMin = new Vector2(-5f, -5f);
    [SerializeField] private Vector2 boundsMax = new Vector2(5f, 5f);

    private GameObject currentPoint;
    private int points;
    private GameObject[] countSprites;
    private readonly List<GameObject> collectedPoints = new List<GameObject>();
    private readonly List<GameObject> collectSquareEffects = new List<GameObject>();
    private float orbitAngle;
    private Vector2 orbitCenter;
    private Vector3 lastCollectPosition;
    private bool hasRespawnPoint;
    private bool isGameActive;
    private Sprite collectSquareSprite;
    private Material collectSquareInvertMaterial;
    private GameObject currentPointMarker;
    private Coroutine currentPointMarkerCoroutine;

    public Vector3 RespawnPoint => hasRespawnPoint ? lastCollectPosition : new Vector3(0f, 0f, -2f);

    void Start()
    {
        countSprites = new GameObject[] { pointCount1, pointCount2, pointCount3 };

        foreach (var cs in countSprites)
        {
            if (cs != null)
            {
                ApplyPointSortingOrder(cs);
                cs.SetActive(false);
            }
        }

        if (mainManager == null)
            mainManager = FindFirstObjectByType<MainManager>();

        ResetGame(false);
    }

    void Update()
    {
        if (!isGameActive || currentPoint == null || player == null) return;

        PointMovementMode movementMode = GetCurrentMovementMode();

        if (movementMode == PointMovementMode.Orbit)
        {
            orbitAngle += orbitSpeed * Time.deltaTime;
            float rad = orbitAngle * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                orbitCenter.x + Mathf.Cos(rad) * orbitRadius,
                orbitCenter.y + Mathf.Sin(rad) * orbitRadius,
                pointZ);
            currentPoint.transform.position = pos;
        }
        else if (movementMode == PointMovementMode.Flee)
        {
            Vector2 playerPos = (Vector2)player.position;
            Vector2 pointPos = (Vector2)currentPoint.transform.position;

            // プレイヤーから一番遠い角を求める
            Vector2 farthestCorner = new Vector2(
                Mathf.Abs(playerPos.x - boundsMin.x) >= Mathf.Abs(playerPos.x - boundsMax.x) ? boundsMin.x : boundsMax.x,
                Mathf.Abs(playerPos.y - boundsMin.y) >= Mathf.Abs(playerPos.y - boundsMax.y) ? boundsMin.y : boundsMax.y
            );

            // ターゲット = プレイヤーと一番遠い角の中間地点
            Vector2 target = (playerPos + farthestCorner) * 0.5f;

            Vector2 fromPlayer = pointPos - playerPos;
            float distToPlayer = fromPlayer.magnitude;
            Vector2 toTarget = target - pointPos;

            Vector2 moveDir;

            if (distToPlayer > 0.01f)
            {
                Vector2 fromPlayerNorm = fromPlayer / distToPlayer;
                Vector2 toTargetNorm = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Vector2.zero;

                // ターゲット方向がプレイヤー方向と重なるか判定
                float dot = Vector2.Dot(toTargetNorm, -fromPlayerNorm);

                if (dot > 0f)
                {
                    // ターゲットがプレイヤーの向こう側 → 迂回する
                    // プレイヤーからの垂直方向（ターゲット寄りの方）を選択
                    Vector2 perp1 = new Vector2(-fromPlayerNorm.y, fromPlayerNorm.x);
                    Vector2 perp = Vector2.Dot(perp1, toTargetNorm) >= 0f ? perp1 : -perp1;

                    // 垂直方向 + プレイヤーから離れる方向をブレンド
                    moveDir = (perp + fromPlayerNorm * playerAvoidWeight).normalized;
                }
                else
                {
                    // プレイヤーが邪魔にならない → ターゲットへ直進
                    moveDir = toTargetNorm;
                }
            }
            else
            {
                moveDir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Vector2.up;
            }

            if (moveDir.sqrMagnitude > 0.01f)
            {
                Vector3 newPos = currentPoint.transform.position + (Vector3)(moveDir * fleeSpeed * Time.deltaTime);
                newPos.x = Mathf.Clamp(newPos.x, boundsMin.x, boundsMax.x);
                newPos.y = Mathf.Clamp(newPos.y, boundsMin.y, boundsMax.y);
                newPos.z = pointZ;
                currentPoint.transform.position = newPos;
            }
        }

        // プレイヤーとポイントの距離判定（XYのみ）
        Vector2 collectDiff = (Vector2)player.position - (Vector2)currentPoint.transform.position;
        if (collectDiff.sqrMagnitude <= collectDistance * collectDistance)
        {
            CollectPoint();
        }
    }

    private int GetPointType()
    {
        return points % 3;
    }

    private PointMovementMode GetCurrentMovementMode()
    {
        return GetPointType() switch
        {
            1 => point2Movement,
            2 => point3Movement,
            _ => point1Movement,
        };
    }

    private void CollectPoint()
    {
        lastCollectPosition = currentPoint.transform.position;
        lastCollectPosition.z = -2f;
        hasRespawnPoint = true;

        GameObject collectedPoint = currentPoint;
        ClearPointMarker();
        currentPoint = null;

        PlayCollectSquareEffect(lastCollectPosition);

        bool willClear = points + 1 >= 3;
        MoveCollectedPointToCountSlot(collectedPoint, points, willClear);

        // チェックポイント保存
        if (shadowManager != null)
            shadowManager.SaveCheckpoint();

        points++;

        // 3ポイント獲得でゲームクリア
        if (points >= 3)
        {
            Debug.Log($"[PointManager] Game Clear! points={points}, mainManager={mainManager}");
            return;
        }

        SpawnPoint();
    }

    private void SpawnPoint()
    {
        Vector2[] positions = GetPointType() switch
        {
            1 => spawnPositions2,
            2 => spawnPositions3,
            _ => spawnPositions1,
        };

        if (positions == null || positions.Length == 0) return;

        int index = Random.Range(0, positions.Length);
        Vector3 pos = new Vector3(positions[index].x, positions[index].y, pointZ);
        currentPoint = Instantiate(pointPrefab, pos, Quaternion.identity);
        ApplyPointSortingOrder(currentPoint);
        AttachPointMarker(currentPoint);
        StartCoroutine(AnimateSpawnPoint(currentPoint.transform));
        orbitAngle = 0f;

        if (GetCurrentMovementMode() == PointMovementMode.Orbit)
        {
            orbitCenter = positions[index];
        }
    }

    private IEnumerator AnimateSpawnPoint(Transform pointTransform)
    {
        if (pointTransform == null)
            yield break;

        Vector3 targetScale = pointTransform.localScale;
        Vector3 startScale = targetScale * Mathf.Max(0f, spawnStartScale);
        float duration = Mathf.Max(0.01f, spawnGrowDuration);
        float elapsed = 0f;

        pointTransform.localScale = startScale;

        while (elapsed < duration && pointTransform != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            pointTransform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            yield return null;
        }

        if (pointTransform != null)
            pointTransform.localScale = targetScale;
    }

    public void ResetGame(bool startImmediately)
    {
        ClearPointMarker();

        if (currentPoint != null)
        {
            Destroy(currentPoint);
            currentPoint = null;
        }

        for (int i = collectedPoints.Count - 1; i >= 0; i--)
        {
            if (collectedPoints[i] != null)
                Destroy(collectedPoints[i]);
        }
        collectedPoints.Clear();

        for (int i = collectSquareEffects.Count - 1; i >= 0; i--)
        {
            if (collectSquareEffects[i] != null)
                Destroy(collectSquareEffects[i]);
        }
        collectSquareEffects.Clear();

        points = 0;
        orbitAngle = 0f;
        lastCollectPosition = Vector3.zero;
        hasRespawnPoint = false;
        isGameActive = startImmediately;

        if (countSprites == null)
            countSprites = new GameObject[] { pointCount1, pointCount2, pointCount3 };

        foreach (var cs in countSprites)
        {
            if (cs != null)
            {
                ApplyPointSortingOrder(cs);
                cs.SetActive(false);
            }
        }

        if (isGameActive)
            SpawnPoint();
    }

    private void AttachPointMarker(GameObject pointObject)
    {
        ClearPointMarker();

        if (!showPointMarker || pointObject == null)
            return;

        EnsureCollectSquareSprite();
        if (collectSquareSprite == null)
            return;

        GameObject markerObject = new GameObject("Point Invert Marker");
        markerObject.transform.SetParent(pointObject.transform, false);
        markerObject.transform.localPosition = new Vector3(0f, 0f, pointMarkerZOffset);
        markerObject.transform.localRotation = Quaternion.Euler(0f, 0f, pointMarkerRotationZ);
        markerObject.transform.localScale = Vector3.one * pointMarkerMinSize;

        SpriteRenderer markerRenderer = markerObject.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = collectSquareSprite;
        markerRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(pointMarkerInvertAmount));
        markerRenderer.sortingOrder = pointSortingOrder + pointMarkerSortingOrderOffset;
        markerRenderer.sharedMaterial = GetCollectSquareInvertMaterial();

        currentPointMarker = markerObject;
        currentPointMarkerCoroutine = StartCoroutine(AnimatePointMarker(markerObject.transform, markerRenderer));
    }

    private IEnumerator AnimatePointMarker(Transform markerTransform, SpriteRenderer markerRenderer)
    {
        float duration = Mathf.Max(0.01f, pointMarkerPulseDuration);

        while (markerTransform != null && markerRenderer != null)
        {
            float phase = Mathf.PingPong(Time.time / duration, 1f);
            float smoothPhase = Mathf.SmoothStep(0f, 1f, phase);
            float size = Mathf.Lerp(pointMarkerMinSize, pointMarkerMaxSize, smoothPhase);
            markerTransform.localScale = Vector3.one * size;
            markerRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(pointMarkerInvertAmount));
            markerRenderer.sortingOrder = pointSortingOrder + pointMarkerSortingOrderOffset;
            yield return null;
        }
    }

    private void ClearPointMarker()
    {
        if (currentPointMarkerCoroutine != null)
        {
            StopCoroutine(currentPointMarkerCoroutine);
            currentPointMarkerCoroutine = null;
        }

        if (currentPointMarker != null)
        {
            Destroy(currentPointMarker);
            currentPointMarker = null;
        }
    }

    private void MoveCollectedPointToCountSlot(GameObject collectedPoint, int countIndex, bool triggerGameClear)
    {
        if (collectedPoint == null)
            return;

        collectedPoints.Add(collectedPoint);

        if (countSprites == null)
            countSprites = new GameObject[] { pointCount1, pointCount2, pointCount3 };

        if (countIndex < 0 || countIndex >= countSprites.Length || countSprites[countIndex] == null)
        {
            if (triggerGameClear)
                NotifyGameClearAfterPointArrived();
            return;
        }

        Transform slot = countSprites[countIndex].transform;
        Vector3 targetPosition = slot.position;
        Vector3 targetScale = slot.lossyScale;

        countSprites[countIndex].SetActive(false);
        StartCoroutine(AnimateCollectedPoint(collectedPoint.transform, targetPosition, targetScale, triggerGameClear));
    }

    private IEnumerator AnimateCollectedPoint(Transform pointTransform, Vector3 targetPosition, Vector3 targetScale, bool triggerGameClear)
    {
        if (pointTransform == null)
            yield break;

        Vector3 startPosition = pointTransform.position;
        Vector3 startScale = pointTransform.lossyScale;
        float duration = Mathf.Max(0.01f, collectMoveDuration);
        float elapsed = 0f;

        while (elapsed < duration && pointTransform != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            pointTransform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
            pointTransform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            yield return null;
        }

        if (pointTransform != null)
        {
            pointTransform.position = targetPosition;
            pointTransform.localScale = targetScale;
        }

        if (triggerGameClear)
            NotifyGameClearAfterPointArrived();
    }

    private void NotifyGameClearAfterPointArrived()
    {
        if (mainManager != null)
            mainManager.OnGameClear();
    }

    private void PlayCollectSquareEffect(Vector3 centerPosition)
    {
        if (!showCollectSquareEffect)
            return;

        EnsureCollectSquareSprite();
        if (collectSquareSprite == null)
            return;

        Vector3 effectPosition = new Vector3(centerPosition.x, centerPosition.y, collectSquareZ);
        GameObject effectObject = new GameObject("Collect Square Effect");
        effectObject.transform.position = effectPosition;
        effectObject.transform.rotation = Quaternion.Euler(0f, 0f, collectSquareRotationZ);
        effectObject.transform.localScale = Vector3.one * collectSquareStartSize;

        SpriteRenderer spriteRenderer = effectObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = collectSquareSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = collectSquareSortingOrder;
        spriteRenderer.sharedMaterial = GetCollectSquareInvertMaterial();

        GameObject restoreObject = new GameObject("Collect Square Restore Effect");
        restoreObject.transform.SetParent(effectObject.transform, false);
        restoreObject.transform.localPosition = Vector3.zero;
        restoreObject.transform.localRotation = Quaternion.identity;
        restoreObject.transform.localScale = Vector3.one;

        SpriteRenderer restoreRenderer = restoreObject.AddComponent<SpriteRenderer>();
        restoreRenderer.sprite = collectSquareSprite;
        restoreRenderer.color = Color.clear;
        restoreRenderer.sortingOrder = collectSquareSortingOrder + 1;
        restoreRenderer.sharedMaterial = GetCollectSquareInvertMaterial();

        collectSquareEffects.Add(effectObject);
        float endSize = collectSquareFillScreen
            ? Mathf.Max(collectSquareEndSize, GetScreenCoveringSquareSize(effectPosition))
            : collectSquareEndSize;
        StartCoroutine(AnimateCollectSquareEffect(effectObject, spriteRenderer, restoreObject.transform, restoreRenderer, endSize));
    }

    private IEnumerator AnimateCollectSquareEffect(
        GameObject effectObject,
        SpriteRenderer spriteRenderer,
        Transform restoreTransform,
        SpriteRenderer restoreRenderer,
        float endSize)
    {
        if (effectObject == null || spriteRenderer == null)
            yield break;

        float duration = Mathf.Max(0.01f, collectSquareEffectDuration);
        float restoreDelay = Mathf.Max(0f, collectSquareRestoreDelay);
        float restoreDuration = Mathf.Max(0.01f, collectSquareRestoreDuration);
        float totalDuration = Mathf.Max(duration, restoreDelay + restoreDuration);
        float elapsed = 0f;

        while (elapsed < totalDuration && effectObject != null && spriteRenderer != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            effectObject.transform.localScale = Vector3.one * Mathf.Lerp(collectSquareStartSize, endSize, smoothT);
            spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(collectSquareInvertAmount));

            if (restoreTransform != null && restoreRenderer != null)
            {
                float restoreT = Mathf.Clamp01((elapsed - restoreDelay) / restoreDuration);
                float restoreSmoothT = Mathf.SmoothStep(0f, 1f, restoreT);
                float parentScale = Mathf.Max(0.0001f, effectObject.transform.localScale.x);
                float restoreWorldSize = Mathf.Lerp(collectSquareStartSize, endSize, restoreSmoothT);
                restoreTransform.localScale = Vector3.one * (restoreWorldSize / parentScale);
                restoreRenderer.color = restoreT > 0f
                    ? new Color(1f, 1f, 1f, Mathf.Clamp01(collectSquareInvertAmount))
                    : Color.clear;
            }

            yield return null;
        }

        collectSquareEffects.Remove(effectObject);
        if (effectObject != null)
            Destroy(effectObject);
    }

    private void EnsureCollectSquareSprite()
    {
        if (collectSquareSprite != null)
            return;

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        collectSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        collectSquareSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private Material GetCollectSquareInvertMaterial()
    {
        if (collectSquareInvertMaterial != null)
            return collectSquareInvertMaterial;

        Shader invertShader = Shader.Find("Custom/ScreenInvertSprite");
        if (invertShader == null)
        {
            Debug.LogWarning("[PointManager] Custom/ScreenInvertSprite shader was not found. Collect square effect will use the default sprite material.");
            return null;
        }

        collectSquareInvertMaterial = new Material(invertShader);
        collectSquareInvertMaterial.hideFlags = HideFlags.HideAndDontSave;
        return collectSquareInvertMaterial;
    }

    private float GetScreenCoveringSquareSize(Vector3 centerPosition)
    {
        Camera cameraToUse = Camera.main;
        if (cameraToUse == null)
            return collectSquareEndSize;

        float zDistance = Mathf.Abs(centerPosition.z - cameraToUse.transform.position.z);
        Vector3[] screenCorners =
        {
            new Vector3(0f, 0f, zDistance),
            new Vector3(Screen.width, 0f, zDistance),
            new Vector3(0f, Screen.height, zDistance),
            new Vector3(Screen.width, Screen.height, zDistance),
        };

        Quaternion inverseRotation = Quaternion.Inverse(Quaternion.Euler(0f, 0f, collectSquareRotationZ));
        float maxLocalDistance = 0f;
        for (int i = 0; i < screenCorners.Length; i++)
        {
            Vector3 cornerWorld = cameraToUse.ScreenToWorldPoint(screenCorners[i]);
            Vector3 localCorner = inverseRotation * (cornerWorld - centerPosition);
            maxLocalDistance = Mathf.Max(maxLocalDistance, Mathf.Abs(localCorner.x), Mathf.Abs(localCorner.y));
        }

        return maxLocalDistance * 2f * Mathf.Max(1f, collectSquareScreenCoverPadding);
    }

    private void ApplyPointSortingOrder(GameObject pointObject)
    {
        if (pointObject == null)
            return;

        SpriteRenderer[] renderers = pointObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingOrder = pointSortingOrder;
        }
    }
}
