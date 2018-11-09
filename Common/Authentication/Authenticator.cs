using System;
using System.Reflection;
using Connections;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Utils;

namespace Authentication
{
    /// <summary>
    /// Provides some common properties for <c>ClientAuthenticator</c> and <c>ServerAuthenticator</c> classes.
    /// </summary>
    public abstract class Authenticator : ILoggable
    {
        public const string MODULE_NAME = "auth";
        
        /// <inheritdoc />
        public abstract event EventHandler<LogEventArgs> OnLogEntry;
        /// <inheritdoc />
        public abstract void RaiseLogEntry(LogEventArgs e);
        
        public static readonly Version Version = Assembly.GetAssembly(typeof(Authenticator)).GetName().Version;
        
        /// <summary>
        /// Handles incoming packets for this Authenticator.
        /// </summary>
        /// <param name="packetType">Packet type</param>
        /// <param name="connectionId">Original connection ID</param>
        /// <param name="data">Data payload</param>
        protected abstract void packetHandler(string packetType, string connectionId, byte[] data);
        
        /// <summary>
        /// Attempts to validate a serialised packet.
        /// If this method succeeds it can be assumed safe to process the packet further.
        /// </summary>
        /// <param name="packetType">Packet type</param>
        /// <param name="data">Serialised packet</param>
        /// <param name="parsedMessage">Parsed packet as an <c>Any</c> message</param>
        /// <param name="typeUrl">Parsed TypeUrl</param>
        /// <returns>The packet was validated successfully</returns>
        protected bool validatePacket(string packetType, byte[] data, out Any parsedMessage, out TypeUrl typeUrl)
        {
            // Initialise out variables
            parsedMessage = null;
            typeUrl = null;
            
            // Make sure the packet is destined for this module, just in case
            if (!packetType.Equals(MODULE_NAME))
            {
                RaiseLogEntry(new LogEventArgs("Got a packet destined for a different module (" + packetType + "), discarding...", LogLevel.DEBUG));
                return false;
            }
            
            // Parse the incoming message
            parsedMessage= Any.Parser.ParseFrom(data);
            
            // Get the TypeUrl from the message to help determine what it actually is
            try
            {
                typeUrl = new TypeUrl(parsedMessage.TypeUrl);
            }
            catch (Exception e)
            {
                RaiseLogEntry(new LogEventArgs("Got a packet with a malformed TypeUrl, discarding...", LogLevel.DEBUG));
                return false;
            }
            
            // Check that the message's namespace matches the one we will be using with our packets
            if (typeUrl.Namespace != "Authentication")
            {
                RaiseLogEntry(new LogEventArgs("Got a packet type from a different namespace than we're expecting (" + typeUrl.Namespace + "), discarding...", LogLevel.DEBUG));
                return false;
            }
            
            // Nothing bad has happened so far, so the packet is clear for further processing
            return true;
        }
    }
}
