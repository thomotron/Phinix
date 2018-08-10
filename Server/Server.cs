using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace PhinixServer
{
    static class Server
    {
        static void Main()
        {
            Connections.NetServer connections = new Connections.NetServer(new IPEndPoint(IPAddress.Any, 16180));
            connections.Start();

            Console.Read();
        }
    }
}
