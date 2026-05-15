using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RankingManager : MonoBehaviour
{
    [SerializeField] private string rankingFilePath = "Assets/Settings/Data/ranking.json";
    [SerializeField] private int maxEntriesToKeep;
    [SerializeField] private int topCount = 5;
    [SerializeField] private Color latestEntryColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color latestEntryMarkColor = new Color(0f, 0f, 0f, 0.67f);

    public int LatestHighlightedLineIndex { get; private set; } = -1;
    public string LatestHighlightedLineText { get; private set; } = string.Empty;
    public Color LatestEntryColor => latestEntryColor;
    public Color LatestEntryMarkColor => latestEntryMarkColor;

    [Serializable]
    private class RankingData
    {
        public List<RankingEntry> entries = new List<RankingEntry>();
    }

    [Serializable]
    private class RankingEntry
    {
        public string id;
        public string name;
        public float clearTimeSeconds;
        public string clearedAt;
    }

    public string RecordClearAndBuildTopText(float clearTimeSeconds)
    {
        RankingData data = LoadRankingData();
        string latestId = Guid.NewGuid().ToString("N");
        data.entries.Add(new RankingEntry
        {
            id = latestId,
            name = DateTime.Now.ToString("H時m分"),
            clearTimeSeconds = clearTimeSeconds,
            clearedAt = DateTime.Now.ToString("o"),
        });

        SortAndTrim(data);
        SaveRankingData(data);
        return BuildTopText(data, latestId, true);
    }

    public string BuildCurrentTopText()
    {
        RankingData data = LoadRankingData();
        SortAndTrim(data);
        return BuildTopText(data, string.Empty, true);
    }

    private RankingData LoadRankingData()
    {
        string path = GetFullPath();
        if (!File.Exists(path))
            return new RankingData();

        try
        {
            string json = File.ReadAllText(path);
            RankingData data = JsonUtility.FromJson<RankingData>(json);
            return data ?? new RankingData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RankingManager] Failed to load ranking: {exception.Message}");
            return new RankingData();
        }
    }

    private void SaveRankingData(RankingData data)
    {
        string path = GetFullPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(data, true));

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    private void SortAndTrim(RankingData data)
    {
        data.entries.RemoveAll(entry => entry == null);
        data.entries.Sort((a, b) => a.clearTimeSeconds.CompareTo(b.clearTimeSeconds));

        if (maxEntriesToKeep > 0 && data.entries.Count > maxEntriesToKeep)
            data.entries.RemoveRange(maxEntriesToKeep, data.entries.Count - maxEntriesToKeep);
    }

    private string BuildTopText(RankingData data, string latestEntryId, bool includeHeader)
    {
        LatestHighlightedLineIndex = -1;
        LatestHighlightedLineText = string.Empty;

        StringBuilder builder = new StringBuilder();
        if (includeHeader)
            builder.AppendLine("ランキング");

        int count = Mathf.Min(topCount, data.entries.Count);
        if (count == 0)
        {
            builder.AppendLine("No records");
            return builder.ToString();
        }

        for (int i = 0; i < count; i++)
        {
            RankingEntry entry = data.entries[i];
            string line = $"{i + 1}位 {FormatTime(entry.clearTimeSeconds)}";
            if (!string.IsNullOrEmpty(latestEntryId) && entry.id == latestEntryId)
            {
                LatestHighlightedLineIndex = includeHeader ? i + 1 : i;
                LatestHighlightedLineText = line;
                line = DecorateLatestEntry(line);
            }

            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    private string DecorateLatestEntry(string line)
    {
        return $"<color=#00000000>{line}</color>";
    }

    private string GetFullPath()
    {
        if (Path.IsPathRooted(rankingFilePath))
            return rankingFilePath;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, rankingFilePath);
    }

    private static string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = (int)(seconds / 60f);
        int wholeSeconds = (int)(seconds % 60f);
        return $"{minutes}分{wholeSeconds}秒";
    }

}
