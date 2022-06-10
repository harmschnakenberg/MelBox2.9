using System.Diagnostics;

namespace MelBox2
{

    static class Log
    {
        private static readonly string AppName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);

        internal static void Info(string message, int id)
        {
            //EventLog.Delete("MelBoxLog", ".");

            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = AppName;
                eventLog.WriteEntry(message, EventLogEntryType.Information, id);
            }
        }

        internal static void Warning(string message, int id)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = AppName;
                eventLog.WriteEntry(message, EventLogEntryType.Warning, id);
            }
        }

        internal static void Error(string message, int id)
        {
            using (EventLog eventLog = new EventLog("Application"))
            {
                eventLog.Source = AppName;
                eventLog.WriteEntry(message, EventLogEntryType.Error, id);
            }
        }

    }
}
