using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace PhinixServer
{
    class Server
    {
        public static Logger Logger = new Logger("");
        public static readonly Version Version = Assembly.GetAssembly(typeof(Server)).GetName().Version;

        static void Main()
        {
            Connections.NetServer connections = new Connections.NetServer(new IPEndPoint(IPAddress.Any, 16180));
            connections.Start();

            Logger.Log(Severity.INFO, string.Format("Phinix server version {0} listening on port {1}", Version, connections.Endpoint.Port));

            CommandInterpreter interpreter = new CommandInterpreter();
            while (true)
            {
                string line = Console.ReadLine();

                List<string> arguments = new List<string>(line.Split(' '));
                string command = arguments.First();
                arguments.RemoveAt(0); // Remove the command from the argument list

                if (command == "exit") // Check this here to avoid other weird workarounds
                {
                    Logger.Log(Severity.INFO, "Server shutting down");
                    break;
                }

                interpreter.Run(command, arguments);
            }
        }
    }

    
}
