using Sdl.Web.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sdl.Web.Modules.Search
{
    public class DxaSearchException : DxaException
    {
        public DxaSearchException(string message, Exception innerException = null)
                : base(message, innerException)
        {
        }
    }
}
