using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhinixServer
{
    public class Logger
    {
        public Severity MinimumDisplaySeverity;

        private string logPath;

        public Logger(string logPath, Severity minimumDisplaySeverity)
        {
            this.logPath = logPath;
            this.MinimumDisplaySeverity = minimumDisplaySeverity;
        }

        public void Log(Severity severity, string message)
        {
            // Only write to the console if the severity meets the minimum
            if (severity >= MinimumDisplaySeverity) Console.WriteLine("[{0:u}][{1}] {2}", DateTime.UtcNow, severity.ToString(), message);
        }
    }

    public enum Severity
    {
        DEBUG,
        INFO,
        WARN,
        ERROR,
        FATAL
    }
}
