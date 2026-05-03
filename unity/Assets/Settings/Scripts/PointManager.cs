using UnityEngine;

public class PointManager : MonoBehaviour
{
    [SerializeField] private GameObject pointPrefab;

    [Header("Count Sprites")]
    [SerializeField] private GameObject pointCount1;
    [SerializeField] private GameObject pointCount2;
    [SerializeField] private GameObject pointCount3;

    [Header("Spawn Positions")]
    [SerializeField] private Vector2[] spawnPositions1;
    [SerializeField] private Vector2[] spawnPositions2;
    [SerializeField] private Vector2[] spawnPositions3;

    [Header("Collection")]
    [SerializeField] private float collectDistance = 0.5f;
    [SerializeField] private Transform player;
    [SerializeField] private float pointZ = 0f;
    [SerializeField] private ShadowManager shadowManager;
    [SerializeField] private MainManager mainManager;

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
    private float orbitAngle;
    private Vector2 orbitCenter;
    private Vector3 lastCollectPosition;

    public Vector3 RespawnPoint => points == 0 ? new Vector3(0f, 0f, -2f) : lastCollectPosition;

    void Start()
    {
        countSprites = new GameObject[] { pointCount1, pointCount2, pointCount3 };

        foreach (var cs in countSprites)
        {
            if (cs != null) cs.SetActive(false);
        }

        if (mainManager == null)
            mainManager = FindFirstObjectByType<MainManager>();

        SpawnPoint();
    }

    void Update()
    {
        if (currentPoint == null || player == null) return;

        int pointType = GetPointType();

        // ポイント2: 指定座標を中心に回転
        if (pointType == 1)
        {
            orbitAngle += orbitSpeed * Time.deltaTime;
            float rad = orbitAngle * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                orbitCenter.x + Mathf.Cos(rad) * orbitRadius,
                orbitCenter.y + Mathf.Sin(rad) * orbitRadius,
                pointZ);
            currentPoint.transform.position = pos;
        }
        // ポイント3: プレイヤーを避けながら中間地点へ逃げる
        else if (pointType == 2)
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

    private void CollectPoint()
    {
        lastCollectPosition = currentPoint.transform.position;
        lastCollectPosition.z = -2f;

        Destroy(currentPoint);
        currentPoint = null;

        if (points < countSprites.Length && countSprites[points] != null)
        {
            countSprites[points].SetActive(true);
        }

        points++;

        // チェックポイント保存
        if (shadowManager != null)
            shadowManager.SaveCheckpoint();

        // 3ポイント獲得でゲームクリア
        if (points >= 3)
        {
            Debug.Log($"[PointManager] Game Clear! points={points}, mainManager={mainManager}");
            if (mainManager != null)
                mainManager.OnGameClear();
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
        orbitAngle = 0f;

        if (GetPointType() == 1)
        {
            orbitCenter = positions[index];
        }
    }
}
