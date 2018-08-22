using System;
using System.Security.Cryptography;

namespace UserManagement
{
    /// <summary>
    /// Represents a user with a unique ID.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The user's Universally Unique IDentifier.
        /// </summary>
        public readonly string Uuid;
        
        /// <summary>
        /// The user's username.
        /// </summary>
        public string Username;

        /// <summary>
        /// Instantiates a new <c>User</c> with the given username and a random UUID.
        /// </summary>
        /// <param name="username">Username</param>
        public User(string username)
        {
            this.Uuid = generateUuid();
            this.Username = username;
        }

        /// <summary>
        /// Instantiates a new <c>User</c> with the given UUID and username.
        /// </summary>
        /// <param name="uuid">UUID</param>
        /// <param name="username">Username</param>
        public User(string uuid, string username)
        {
            this.Uuid = uuid;
            this.Username = username;
        }

        /// <summary>
        /// Generates an RFC 4122 compliant version 4 UUID.
        /// </summary>
        /// <returns>Generated UUID</returns>
        /// 
        /// The contents of this method were originally published by André N. Klingsheim on 2012/07/29 17:07 UTC
        /// Retrieved on 2018/08/21 09:19 UTC from https://www.dotnetnoob.com/2012/07/generating-secure-guids.html
        /// Modifications:
        ///   Appended .ToString() to the return line to conform with the surrounding method
        ///   Removed using block surrounding the two RNGCryptoServiceProvider lines due to it missing Dispose()
        private string generateUuid()
        {
            byte[] bytes = { 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00 };

            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bytes);

            uint time = BitConverter.ToUInt32(bytes,0);
            ushort time_mid = BitConverter.ToUInt16(bytes,4);
            ushort  time_hi_and_ver = BitConverter.ToUInt16(bytes,6);
            time_hi_and_ver = (ushort)((time_hi_and_ver | 0x4000) & 0x4FFF);
            
            bytes[8] = (byte)((bytes[8] | 0x80) & 0xBF);
            
            return new Guid(time,time_mid,time_hi_and_ver,
                bytes[8],bytes[9],bytes[10],bytes[11],bytes[12],bytes[13],
                bytes[14],bytes[15]).ToString();
        }
    }
}