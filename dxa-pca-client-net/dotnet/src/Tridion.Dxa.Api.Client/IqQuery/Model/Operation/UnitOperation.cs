using Tridion.Dxa.Api.Client.IqQuery.API;

namespace Tridion.Dxa.Api.Client.IqQuery.Model.Operation
{
    /// <summary>
    /// Unit Operation
    /// </summary>
    public class UnitOperation : BaseOperation
    {
        public UnitOperation(IQuery query) : base(query, BooleanOperationType.Unit)
        {
        }
    }
}
