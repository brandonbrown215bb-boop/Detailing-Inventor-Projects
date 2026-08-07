using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace QuestBoard.UI.Services
{
    public class QuestCliService
    {
        private readonly string _repoRoot;

        public QuestCliService(string repoRoot)
        {
            _repoRoot = repoRoot;
        }

        public async Task<(bool success, string output)> RunCliAsync(string arguments)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string scriptPath = Path.Combine(_repoRoot, ".questboard", "quest.py");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{scriptPath}\" {arguments}",
                        WorkingDirectory = _repoRoot,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var proc = new Process { StartInfo = psi };
                    var sbOut = new StringBuilder();
                    var sbErr = new StringBuilder();

                    proc.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();

                    string stdout = sbOut.ToString();
                    string stderr = sbErr.ToString();

                    if (proc.ExitCode == 0)
                    {
                        return (true, string.IsNullOrWhiteSpace(stdout) ? "Operation succeeded." : stdout);
                    }
                    else
                    {
                        string errStr = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
                        return (false, string.IsNullOrWhiteSpace(errStr) ? $"Process exited with code {proc.ExitCode}" : errStr);
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });
        }

        public async Task<(bool success, string output)> AddQuestAsync(string title, string context, string nextAction, string priority, string actor)
        {
            string args = $"add \"{Escape(title)}\" --context \"{Escape(context)}\" --next \"{Escape(nextAction)}\" --priority {priority} --actor \"{Escape(actor)}\"";
            return await RunCliAsync(args);
        }

        public async Task<(bool success, string output)> ClaimQuestAsync(string questId, string actor)
        {
            string args = $"claim {questId} --actor \"{Escape(actor)}\"";
            return await RunCliAsync(args);
        }

        public async Task<(bool success, string output)> HandoffQuestAsync(string questId, string actor, string nextAction, string note)
        {
            string args = $"handoff {questId} --actor \"{Escape(actor)}\" --next \"{Escape(nextAction)}\" --note \"{Escape(note)}\"";
            return await RunCliAsync(args);
        }

        public async Task<(bool success, string output)> MoveQuestAsync(string questId, string status, string actor, string nextAction, string note, string? blocker = null)
        {
            string blockerArg = !string.IsNullOrWhiteSpace(blocker) ? $"--blocker \"{Escape(blocker)}\"" : "";
            string args = $"move {questId} {status} --actor \"{Escape(actor)}\" --next \"{Escape(nextAction)}\" --note \"{Escape(note)}\" {blockerArg}";
            return await RunCliAsync(args);
        }

        public async Task<(bool success, string output)> FinishQuestAsync(string questId, string actor, string note)
        {
            string args = $"finish {questId} --actor \"{Escape(actor)}\" --note \"{Escape(note)}\"";
            return await RunCliAsync(args);
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\"", "\\\"");
        }
    }
}
