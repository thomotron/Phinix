using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using NetworkCommsDotNet;
using ProtoBuf;

namespace PhinixServer
{
    public class Connections
    {
        public static void Main()
        {
            NetworkComms.AppendGlobalConnectionEstablishHandler(connection =>
            {
                
            });

            NetworkComms.AppendGlobalConnectionCloseHandler(connection =>
            {

            });

            NetworkComms.AppendGlobalIncomingPacketHandler<byte[]>("Phinix", (header, connection, incomingObject) =>
            {
                MemoryStream ms = new MemoryStream(incomingObject);
                object Object = Serializer.Deserialize<object>(ms);
            });
        }
    }

    [ProtoContract]
    public class AuthNotify
    {
        public enum AuthType
        {
            UUID,
            UserPass,
            OAuth,
            OpenID
        }

        [ProtoMember(1)]
        public AuthType AuthMethod;

        [ProtoMember(2)]
        public int Nonce;

        [ProtoMember(3)]
        public string Redirect;
    }

    [ProtoContract]
    public class AuthRequest
    {
        [ProtoMember(1)]
        public long UUID;

        [ProtoMember(2)]
        public string ClientCode;

        [ProtoMember(3)]
        public string Username;

        [ProtoMember(4)]
        public byte[] PasswordHash;
    }
}
