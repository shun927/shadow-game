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

    [Serializable]
    private class RankingData
    {
        public List<RankingEntry> entries = new List<RankingEntry>();
    }

    [Serializable]
    private class RankingEntry
    {
        public string name;
        public float clearTimeSeconds;
        public string clearedAt;
    }

    public string RecordClearAndBuildTopText(float clearTimeSeconds)
    {
        RankingData data = LoadRankingData();
        data.entries.Add(new RankingEntry
        {
            name = DateTime.Now.ToString("H時m分"),
            clearTimeSeconds = clearTimeSeconds,
            clearedAt = DateTime.Now.ToString("o"),
        });

        SortAndTrim(data);
        SaveRankingData(data);
        return BuildTopText(data, clearTimeSeconds, true);
    }

    public string BuildCurrentTopText()
    {
        RankingData data = LoadRankingData();
        SortAndTrim(data);
        return BuildTopText(data, 0f, false);
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

    private string BuildTopText(RankingData data, float latestClearTimeSeconds, bool includeLatestTime)
    {
        StringBuilder builder = new StringBuilder();
        if (includeLatestTime)
            builder.AppendLine($"CLEAR TIME {FormatTime(latestClearTimeSeconds)}");

        builder.AppendLine();

        int count = Mathf.Min(topCount, data.entries.Count);
        if (count == 0)
        {
            builder.AppendLine("No records");
            return builder.ToString();
        }

        for (int i = 0; i < count; i++)
        {
            RankingEntry entry = data.entries[i];
            builder.AppendLine($"{i + 1}位 {FormatTime(entry.clearTimeSeconds)}");
        }

        return builder.ToString();
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
