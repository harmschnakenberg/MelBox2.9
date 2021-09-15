using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MelBox2
{
    class Scheduler
    {
        public static string TaskName { get; set; } = "MelBoxWatchDog";
        static readonly string taskPath = System.Reflection.Assembly.GetAssembly(typeof(Program)).Location;  // Diese exe

        internal static void CeckOrCreateWatchDog() 
        {
            if (!HasTask(TaskName) && !CreateSchedulerTask(15, TaskName, taskPath))                
                    Log.Error("Es konnte kein Task zur Überwachung von MelBox erstellt werden.", 1211);                
        }

        private static bool HasTask(string taskname)
        {           
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Normal,
                Arguments = "/query /TN \\KKT\\" + taskname,
                RedirectStandardOutput = true
            };

            try
            {
                using (Process process = Process.Start(start))
                {
                    // Read in all the text from the process with the StreamReader.
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string stdout = reader.ReadToEnd();

                        if (stdout.Contains(taskname))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

            }
            catch
            {
                Log.Error("Der Windows-TaskScheduler konnte nicht ausgelesen werden", 1204);
                return false;
            }
        }

        private static bool CreateSchedulerTask(int intervallMinutes, string scheduledTaskName, string taskPath)
        {            
            string startTime = DateTime.Now.AddMinutes(5).ToShortTimeString();
           
            string schtasksCommand = String.Format("/Create /SC Minute /MO {0} /TN \\KKT\\{1} /TR \"{2}\" /ST {3}", intervallMinutes, scheduledTaskName, taskPath, startTime);

            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Normal,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = schtasksCommand
            };

            using (Process process = Process.Start(start))
            {
                // Read the error stream first and then wait.
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                //Check for an error
                if (!String.IsNullOrEmpty(error))
                {
                    //Console.WriteLine("Fehler beim erstellen des SchedulerTasks {0} - Fehlertext: {1}", scheduledTaskName, error);
                    return false;
                }
                else
                {
                    string infoText = "Es wurde ein neuer Windows-SchedulerTask zur Überwachung von MelBox2 erstelt.";
                    Log.Info(infoText, 61301);
                    Console.WriteLine(infoText);
                    return true;
                }
            }
        }

    }
}
