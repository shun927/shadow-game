using UnityEngine;
using TMPro;

public class MainManager : MonoBehaviour
{
    [SerializeField] private GameObject clearText;
    [SerializeField] private Player player;
    [SerializeField] private ShadowManager shadowManager;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private PointManager pointManager;
    [SerializeField] private StartButton startButton;

    void Start()
    {
        if (clearText != null)
            clearText.SetActive(false);

        if (timeManager != null)
            timeManager.StopTimer();

        if (pointManager != null)
            pointManager.ResetGame(false);

        if (startButton != null)
            startButton.Show();
    }

    public void StartGame()
    {
        if (clearText != null)
            clearText.SetActive(false);

        if (player != null)
            player.enabled = true;

        if (shadowManager != null)
            shadowManager.enabled = true;

        if (timeManager != null)
            timeManager.ResetTimer();

        if (pointManager != null)
            pointManager.ResetGame(true);

        if (startButton != null)
            startButton.Hide();
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

        if (shadowManager != null)
            shadowManager.enabled = false;

        if (pointManager != null)
            pointManager.ResetGame(false);

        if (startButton != null)
            startButton.Show();
    }
}
