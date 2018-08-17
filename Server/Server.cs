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
        static void Main()
        {
            Connections.NetServer connections = new Connections.NetServer(new IPEndPoint(IPAddress.Any, 16180));
            connections.Start();

            CommandInterpreter interpreter = new CommandInterpreter();
            while (true)
            {
                string line = Console.ReadLine();

                List<string> arguments = new List<string>(line.Split(' '));
                string command = arguments.First();
                arguments.RemoveAt(0); // Remove the command from the argument list

                if (command == "exit") break; // Check this here to avoid other weird workarounds

                interpreter.Run(command, arguments);
            }
        }
    }

    
}
