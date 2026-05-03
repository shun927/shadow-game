using UnityEngine;
using TMPro;

public class MainManager : MonoBehaviour
{
    [SerializeField] private GameObject clearText;
    [SerializeField] private Player player;
    [SerializeField] private ShadowManager shadowManager;
    [SerializeField] private TimeManager timeManager;

    void Start()
    {
        if (clearText != null)
            clearText.SetActive(false);
    }

    public void OnGameClear()
    {
        Debug.Log("[MainManager] OnGameClear called!");

        // クリアテキスト表示
        if (clearText != null)
            clearText.SetActive(true);

        // タイマー停止
        if (timeManager != null)
            timeManager.StopTimer();

        // 操作停止
        if (player != null)
            player.enabled = false;

        if (shadowManager != null)
            shadowManager.enabled = false;
    }
}
