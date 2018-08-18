using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhinixServer
{
    public class Logger
    {
        private string logPath;

        public Logger(string logPath)
        {
            this.logPath = logPath;
        }

        public void Log(Severity severity, string message)
        {
            Console.WriteLine("[{0:u}][{1}] {2}", DateTime.UtcNow, severity.ToString(), message);
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
