using System.Text;
using XFramework.Console;

namespace XFramework.Fsm
{
    public class FsmConsoleCommands : GMCommand
    {
        [GMCommand("fsm_list")]
        public static string ListFsms()
        {
            if (!GameEntry.IsModuleLoaded<FsmManager>())
            {
                string unloaded = "[FSM] FsmManager is not loaded.";
                XConsole.Log(unloaded);
                return unloaded;
            }

            var entries = FsmManager.Instance.GetDebugEntries();
            var builder = new StringBuilder();
            builder.AppendLine($"[FSM] Active Count: {entries.Count}");

            for (int i = 0; i < entries.Count; i++)
            {
                FsmDebugEntry entry = entries[i];
                builder.Append("- ");
                builder.Append(entry.Key);
                builder.Append(" | ");
                builder.Append(entry.Scope);
                builder.Append(" | ");
                builder.Append(string.IsNullOrEmpty(entry.CurrentStateName) ? "<Stopped>" : entry.CurrentStateName);
                builder.Append(" | Payload: ");
                builder.Append(entry.LastPayloadSummary);
                builder.AppendLine();
            }

            string text = builder.ToString().TrimEnd();
            XConsole.Log(text);
            return text;
        }
    }
}
