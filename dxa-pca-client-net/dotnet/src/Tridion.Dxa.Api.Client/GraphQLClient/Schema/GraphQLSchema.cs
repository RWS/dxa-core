﻿using System.Collections.Generic;

namespace Tridion.Dxa.Api.Client.GraphQLClient.Schema
{    
    public class GraphQLSchema
    {
        public GraphQLQueryType QueryType { get; set; }
        public object MutationType { get; set; }
        public object SubscriptionType { get; set; }
        public List<GraphQLSchemaType> Types { get; set; }
        public object Directives { get; set; }
        public object Args { get; set; }
    }
}
