using System;
using System.Collections.Generic;
using System.Text;

namespace Transmission.API.RPC
{
    /// <summary>
    /// Thrown when the session id handshake fails.
    /// </summary>
    public class SessionIdException : Exception
    {
        public SessionIdException() : base("Session ID Error") { }
    }
}
