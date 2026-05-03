using UnityEngine;
using TMPro;

public class TimeManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI timerText;

    private float elapsedTime;
    private bool isRunning = true;

    void Update()
    {
        if (!isRunning) return;

        elapsedTime += Time.deltaTime;

        if (timerText != null)
        {
            int minutes = (int)(elapsedTime / 60f);
            int seconds = (int)(elapsedTime % 60f);
            int milliseconds = (int)((elapsedTime * 100f) % 100f);
            timerText.text = $"{minutes:00}:{seconds:00}.{milliseconds:00}";
        }
    }

    public void StopTimer() => isRunning = false;
    public void StartTimer() => isRunning = true;
    public void ResetTimer() { elapsedTime = 0f; isRunning = true; }
    public float ElapsedTime => elapsedTime;
}
