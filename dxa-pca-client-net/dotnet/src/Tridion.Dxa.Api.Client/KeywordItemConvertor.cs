using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Tridion.Dxa.Api.Client.ContentModel;

namespace Tridion.Dxa.Api.Client.Converters
{
    public class KeywordItemConvertor : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Keyword) || objectType == typeof(ITaxonomyItem);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jsonObject = JObject.Load(reader);

            // Create a concrete Keyword instance
            var keyword = new Keyword();

            // Manually populate the properties
            serializer.Populate(jsonObject.CreateReader(), keyword);

            // Handle children specifically to ensure they are concrete Keyword objects
            if (jsonObject["children"] != null && jsonObject["children"]["edges"] != null)
            {
                var childrenEdges = jsonObject["children"]["edges"];
                if (childrenEdges is JArray edgesArray)
                {
                    keyword.Children = new TaxonomyItemConnection
                    {
                        Edges = new List<TaxonomyItemEdge>()
                    };

                    foreach (var edge in edgesArray)
                    {
                        if (edge["node"] != null)
                        {
                            var childKeyword = serializer.Deserialize<Keyword>(edge["node"].CreateReader());
                            keyword.Children.Edges.Add(new TaxonomyItemEdge
                            {
                                Node = childKeyword
                            });
                        }
                    }
                }
            }

            return keyword;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}