using System;
using System.Collections.Generic;
using System.Text;

namespace Transmission.API.RPC
{
    public class SessionIdException : Exception
    {
        public SessionIdException() : base("Session ID Error") { }
    }
}
