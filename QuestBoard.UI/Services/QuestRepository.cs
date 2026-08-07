using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using QuestBoard.UI.Models;

namespace QuestBoard.UI.Services
{
    public class QuestRepository : IDisposable
    {
        private readonly string _questsDirectory;
        private FileSystemWatcher? _watcher;
        private static readonly Regex StateBlockRegex = new Regex(@"<!--\s*questboard:state\s*([\s\S]*?)\s*-->", RegexOptions.Compiled);

        public event EventHandler? QuestsChanged;

        public QuestRepository(string repoRoot)
        {
            _questsDirectory = Path.Combine(repoRoot, ".questboard", "quests");
            InitWatcher();
        }

        private void InitWatcher()
        {
            try
            {
                if (Directory.Exists(_questsDirectory))
                {
                    _watcher = new FileSystemWatcher(_questsDirectory, "*.md")
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                        EnableRaisingEvents = true
                    };

                    _watcher.Changed += OnFileChanged;
                    _watcher.Created += OnFileChanged;
                    _watcher.Deleted += OnFileChanged;
                    _watcher.Renamed += OnFileChanged;
                }
            }
            catch
            {
                // Failsafe: Ignore file watcher driver failures on OneDrive/cloud mapped folders
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                QuestsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        public List<QuestModel> LoadAllQuests()
        {
            var list = new List<QuestModel>();
            if (!Directory.Exists(_questsDirectory))
                return list;

            try
            {
                var files = Directory.GetFiles(_questsDirectory, "*.md");
                foreach (var file in files)
                {
                    var quest = ParseQuestFile(file);
                    if (quest != null)
                    {
                        list.Add(quest);
                    }
                }

                // Sort: Priority high first, then active, then updated date descending
                list.Sort((a, b) =>
                {
                    int statusScoreA = GetStatusScore(a.Status);
                    int statusScoreB = GetStatusScore(b.Status);
                    if (statusScoreA != statusScoreB) return statusScoreA.CompareTo(statusScoreB);
                    return string.Compare(b.UpdatedAt, a.UpdatedAt, StringComparison.Ordinal);
                });
            }
            catch { }

            return list;
        }

        private static int GetStatusScore(string status)
        {
            return status.ToLowerInvariant() switch
            {
                "active" => 1,
                "ready" => 2,
                "review" => 3,
                "blocked" => 4,
                "inbox" => 5,
                "parked" => 6,
                "done" => 7,
                _ => 8
            };
        }

        public static QuestModel? ParseQuestFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                var match = StateBlockRegex.Match(content);
                if (match.Success)
                {
                    string json = match.Groups[1].Value.Trim();
                    var model = JsonSerializer.Deserialize<QuestModel>(json);
                    if (model != null)
                    {
                        model.FilePath = filePath;
                        return model;
                    }
                }
            }
            catch
            {
                // Fallback for corrupted/reading files
            }
            return null;
        }

        public void Dispose()
        {
            try
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                }
            }
            catch { }
        }
    }
}
