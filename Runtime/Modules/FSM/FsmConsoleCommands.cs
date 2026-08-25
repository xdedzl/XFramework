using System.Text;
using XFramework.Command;

namespace XFramework.Fsm
{
    public class FsmConsoleCommands : XCommandGroup
    {
        [XCommandEntry("fsm_list", mode = XCommandMode.Both)]
        public static string ListFsms()
        {
            if (!GameEntry.IsModuleLoaded<FsmManager>())
            {
                string unloaded = "[FSM] FsmManager is not loaded.";
                XCommand.Log(unloaded);
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
            XCommand.Log(text);
            return text;
        }
    }
}
