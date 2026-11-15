using System;
using System.Collections.Generic;
using System.Text;

namespace Transmission.API.RPC
{
    public class RequestFailedException : Exception
    {
        public RequestFailedException(string description) : base(description) { }
    }
}
