using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace PhinixServer
{
    class Server
    {
        private const string CONFIG_FILE = "server.conf";

        public static Config Config;
        public static Logger Logger;
        public static readonly Version Version = Assembly.GetAssembly(typeof(Server)).GetName().Version;

        static void Main()
        {
            Config = Config.Load(CONFIG_FILE);
            Logger = new Logger(Config.LogPath, Config.DisplayVerbosity, Config.LogVerbosity);

            Connections.NetServer connections = new Connections.NetServer(new IPEndPoint(Config.Address, Config.Port));
            connections.Start();

            Logger.Log(Verbosity.INFO, string.Format("Phinix server version {0} listening on port {1}", Version, connections.Endpoint.Port));

            CommandInterpreter interpreter = new CommandInterpreter();
            while (true)
            {
                string line = Console.ReadLine();

                if (line == null) continue;

                List<string> arguments = new List<string>(line.Split(' '));
                string command = arguments.First();
                arguments.RemoveAt(0); // Remove the command from the argument list

                if (command == "exit") // Check this here to avoid other weird workarounds
                {
                    Logger.Log(Verbosity.INFO, "Server shutting down");
                    Config.Save(CONFIG_FILE);
                    break;
                }

                interpreter.Run(command, arguments);
            }
        }
    }
}
