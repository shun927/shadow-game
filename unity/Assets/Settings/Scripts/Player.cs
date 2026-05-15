using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    [Header("Orbit Sprite")]
    [SerializeField] private Transform orbitSprite;
    [SerializeField] private float orbitRadius = 1.5f;
    [SerializeField] private float orbitSpeed = 180f; // degrees per second
    [SerializeField] private float spriteZ = 0f;
    [SerializeField] private int orbitSpriteSortingOrder = 50;

    [Header("Move Bounds")]
    [SerializeField] private Vector2 boundsMin = new Vector2(-10f, -5f);
    [SerializeField] private Vector2 boundsMax = new Vector2(10f, 5f);

    [Header("Squeeze Effect")]
    [SerializeField] private Transform squeezeTarget;
    [SerializeField] private float squeezeScale = 0.5f;
    [SerializeField] private float squeezeLerp = 10f;

    [Header("Input Debug")]
    [SerializeField] private bool logLeftClickInput = true;

    private float currentAngle;
    private Vector3 originalScale;
    private float lastMoveAngle;
    private bool isSqueezing;
    private bool previousLeftClick;
    private SpriteRenderer[] childRenderers;
    private Color[] originalColors;

    void Start()
    {
        if (squeezeTarget != null)
            originalScale = squeezeTarget.localScale;

        ApplyOrbitSpriteSorting();
        CacheChildRenderers();
    }

    private void CacheChildRenderers()
    {
        childRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[childRenderers.Length];
        for (int i = 0; i < childRenderers.Length; i++)
        {
            originalColors[i] = childRenderers[i].color;
        }
    }

    void Update()
    {
        bool leftClick = Input.GetMouseButton(0);
        if (logLeftClickInput && leftClick != previousLeftClick)
        {
            Debug.Log(leftClick ? "Left click input: pressed" : "Left click input: released");
        }
        previousLeftClick = leftClick;

        // 左クリック中でなく、スクイーズ復帰も完了していればスプライトを回転させる
        if (!leftClick && !isSqueezing)
        {
            currentAngle += orbitSpeed * Time.deltaTime;
        }

        // 左クリック中はスプライトの方向に移動（XYのみ）
        if (leftClick && orbitSprite != null)
        {
            Vector3 diff = orbitSprite.position - transform.position;
            diff.z = 0f;
            Vector3 moveDir = diff.normalized;
            transform.position += moveDir * speed * Time.deltaTime;

            // 進行方向に細くなる（ローカルY軸を縮小）
            lastMoveAngle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            if (squeezeTarget != null)
            {
                squeezeTarget.rotation = Quaternion.Euler(0f, 0f, lastMoveAngle);
                Vector3 targetScale = new Vector3(originalScale.x, originalScale.y * squeezeScale, originalScale.z);
                squeezeTarget.localScale = Vector3.Lerp(squeezeTarget.localScale, targetScale, squeezeLerp * Time.deltaTime);
            }
            isSqueezing = true;
        }
        else if (isSqueezing)
        {
            // 移動終了後：進行方向の回転を維持したままスケールを復帰
            if (squeezeTarget != null)
            {
                squeezeTarget.rotation = Quaternion.Euler(0f, 0f, lastMoveAngle);
                squeezeTarget.localScale = Vector3.Lerp(squeezeTarget.localScale, originalScale, squeezeLerp * Time.deltaTime);

                // スケールが十分に戻ったら復帰完了
                if (Vector3.Distance(squeezeTarget.localScale, originalScale) < 0.01f)
                {
                    squeezeTarget.localScale = originalScale;
                    squeezeTarget.rotation = Quaternion.identity;
                    isSqueezing = false;
                }
            }
            else
            {
                isSqueezing = false;
            }
        }

        // 移動範囲を制限
        Vector3 clamped = transform.position;
        clamped.x = Mathf.Clamp(clamped.x, boundsMin.x, boundsMax.x);
        clamped.y = Mathf.Clamp(clamped.y, boundsMin.y, boundsMax.y);
        transform.position = clamped;

        // スプライトの位置を更新（全てのtransform変更後に行う）
        if (orbitSprite != null)
        {
            ApplyOrbitSpriteSorting();

            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;
            Vector3 pos = transform.position + offset;
            pos.z = spriteZ;
            orbitSprite.position = pos;

            // スプライトが常にプレイヤー（中心）を向くように回転
            Vector3 dir = transform.position - orbitSprite.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            orbitSprite.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void ApplyOrbitSpriteSorting()
    {
        if (orbitSprite == null)
            return;

        SpriteRenderer[] orbitRenderers = orbitSprite.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < orbitRenderers.Length; i++)
        {
            if (orbitRenderers[i] == null)
                continue;

            orbitRenderers[i].sortingOrder = orbitSpriteSortingOrder;
        }
    }

    /// <summary>
    /// プレイヤーの子オブジェクトのアルファ値を設定
    /// </summary>
    public void SetChildrenAlpha(float alpha)
    {
        if (childRenderers == null || originalColors == null)
            CacheChildRenderers();

        alpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < childRenderers.Length; i++)
        {
            Color c = originalColors[i];
            c.a = originalColors[i].a * alpha;
            childRenderers[i].color = c;
        }
    }

    /// <summary>
    /// プレイヤーの子オブジェクトのアルファ値をリセット
    /// </summary>
    public void ResetChildrenAlpha()
    {
        if (childRenderers == null || originalColors == null)
            CacheChildRenderers();

        for (int i = 0; i < childRenderers.Length; i++)
        {
            childRenderers[i].color = originalColors[i];
        }
    }

    public void SetVisible(bool visible)
    {
        if (childRenderers == null || originalColors == null)
            CacheChildRenderers();

        for (int i = 0; i < childRenderers.Length; i++)
        {
            childRenderers[i].enabled = visible;
        }
    }

    public void ResetMotionVisuals()
    {
        isSqueezing = false;

        if (squeezeTarget == null)
            return;

        if (originalScale == Vector3.zero)
            originalScale = squeezeTarget.localScale;

        squeezeTarget.localScale = originalScale;
        squeezeTarget.rotation = Quaternion.identity;
    }
}
