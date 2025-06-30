using System;

namespace Tridion.Dxa.Api.Client.Exceptions
{
    public class ApiException : Exception
    {
        public ApiException(string msg) : base(msg)
        {
        }

        public ApiException(string msg, Exception ex) : base(msg, ex)
        {
        }
    }
}
