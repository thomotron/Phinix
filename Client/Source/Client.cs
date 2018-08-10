using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Connections;

namespace PhinixClient
{
    public class Client
    {
        public static void Main()
        {
            Connections.Client client = new Connections.Client();
            client.TryConnect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 16180));

            Console.Read();
        }
    }
}
