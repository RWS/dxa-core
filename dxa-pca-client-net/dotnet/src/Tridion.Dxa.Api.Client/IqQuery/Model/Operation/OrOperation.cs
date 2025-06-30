using Tridion.Dxa.Api.Client.IqQuery.API;

namespace Tridion.Dxa.Api.Client.IqQuery.Model.Operation
{
    /// <summary>
    /// Or Operation
    /// </summary>
    public class OrOperation : BaseOperation
    {
        public OrOperation(IQuery query) : base(query, BooleanOperationType.Or)
        {
        }
    }
}
