using System;
using System.Collections.Generic;
using System.Text;

namespace Transmission.API.RPC
{
    /// <summary>
    /// Thrown when Transmission answers with a result other than success.
    /// </summary>
    public class RequestFailedException : Exception
    {
        public RequestFailedException(string? description) : base(description) { }
    }
}
