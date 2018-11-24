using System;

namespace Authentication
{
    public class UserNoLongerExistsException : Exception
    {
        public override string Message => string.Format("Tried to get details for user with UUID \"{0}\", except that user does not exist.", Uuid);

        public string Uuid;

        public UserNoLongerExistsException(string uuid)
        {
            this.Uuid = uuid;
        }
    }
}