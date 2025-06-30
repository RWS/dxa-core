using Sdl.Web.Common.Models;
using System.Collections.Generic;

namespace Sdl.Web.Common.Interfaces
{
    public interface IQueryProvider
    {
        bool HasMore { get; }
        IEnumerable<string> ExecuteQuery(SimpleBrokerQuery queryParams);
    }
}
