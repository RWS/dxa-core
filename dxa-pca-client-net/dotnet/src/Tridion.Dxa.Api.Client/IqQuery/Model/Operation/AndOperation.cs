using Tridion.Dxa.Api.Client.IqQuery.API;

namespace Tridion.Dxa.Api.Client.IqQuery.Model.Operation
{
    /// <summary>
    /// And Operation
    /// </summary>
    public class AndOperation : BaseOperation
    {
        public AndOperation(IQuery query) : base(query, BooleanOperationType.And)
        {
        }
    }
}
