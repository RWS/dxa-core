using System.Collections.Generic;
using System.Net;

namespace Tridion.Dxa.Api.Client.HttpClient
{
    /// <summary>
    /// Http Headers
    /// </summary>
    public class HttpHeaders : Dictionary<string, object>
    {
        public HttpHeaders()
        {
        }

        public HttpHeaders(HttpHeaders headers)
        {
            foreach (var x in headers)
            {
                Add(x.Key, x.Value);
            }
        }

        public HttpHeaders(WebHeaderCollection headers)
        {
            foreach (var x in headers.AllKeys)
            {
                Add(x, headers[x]);
            }
        }
    }
}
