using System;

namespace Tridion.Dxa.Api.Client.CodeGen
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
                    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Interface |
                    AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class GraphQLFieldAttribute : Attribute
    {

    }
}
