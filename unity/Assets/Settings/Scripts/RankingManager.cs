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
    [SerializeField] private bool publishRankingForPages = true;
    [SerializeField] private string pagesRankingFilePath = "docs/ranking-data.json";
    [SerializeField] private bool autoPushPagesRanking = true;
    [SerializeField] private string autoPushRemote = "origin";
    [SerializeField] private string autoPushBranch = "seitaro";
    [SerializeField] private string autoPushCommitMessage = "Update ranking data";
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
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
        SavePagesRankingData(json);

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    private void SavePagesRankingData(string json)
    {
        if (!publishRankingForPages || string.IsNullOrWhiteSpace(pagesRankingFilePath))
            return;

        string pagesPath = GetProjectPath(pagesRankingFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(pagesPath));
        File.WriteAllText(pagesPath, json);

        if (autoPushPagesRanking)
            PushPagesRankingData();
    }

    private void PushPagesRankingData()
    {
        string projectRoot = GetProjectRoot();
        string rankingPath = NormalizeGitPath(pagesRankingFilePath);

        if (!RunGit(projectRoot, "add", rankingPath))
            return;

        if (RunGitExitCode(projectRoot, false, "diff", "--cached", "--quiet") != 0)
        {
            if (!RunGit(projectRoot, "commit", "-m", autoPushCommitMessage))
                return;
        }

        RunGit(projectRoot, "push", autoPushRemote, autoPushBranch);
    }

    private bool RunGit(string workingDirectory, params string[] arguments)
    {
        return RunGitExitCode(workingDirectory, true, arguments) == 0;
    }

    private int RunGitExitCode(string workingDirectory, bool logFailure, params string[] arguments)
    {
        try
        {
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = BuildProcessArguments(arguments),
            };

            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0 && logFailure)
                    Debug.LogWarning($"[RankingManager] git {string.Join(" ", arguments)} failed: {process.StandardError.ReadToEnd()}");

                return process.ExitCode;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RankingManager] Failed to run git: {exception.Message}");
            return -1;
        }
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

        return GetProjectPath(rankingFilePath);
    }

    private string GetProjectPath(string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath))
            return relativeOrAbsolutePath;

        return Path.Combine(GetProjectRoot(), relativeOrAbsolutePath);
    }

    private string GetProjectRoot()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    private static string NormalizeGitPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string BuildProcessArguments(string[] arguments)
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
                builder.Append(' ');

            builder.Append(QuoteProcessArgument(arguments[i]));
        }

        return builder.ToString();
    }

    private static string QuoteProcessArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "\"\"";

        if (argument.IndexOfAny(new[] { ' ', '\t', '"', '\\' }) < 0)
            return argument;

        return $"\"{argument.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = (int)(seconds / 60f);
        int wholeSeconds = (int)(seconds % 60f);
        return $"{minutes}分{wholeSeconds}秒";
    }

}
