using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Core.JSON.NET
{
    public class Serializer
    {
        public enum TypeInfoDecoration
        {
            None,
            JsonNet,
            Custom
        }

        public static JsonSerializerSettings DefaultSettings => new JsonSerializerSettings()
        {
            ContractResolver = new CustomResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
        };

        public static JsonSerializerSettings DefaultSettingsWithTypeInfo
         => new JsonSerializerSettings()
         {
             ContractResolver = new CustomResolver(),
             TypeNameHandling = TypeNameHandling.All,
             ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
             MissingMemberHandling = MissingMemberHandling.Ignore,
             NullValueHandling = NullValueHandling.Ignore,
             ObjectCreationHandling = ObjectCreationHandling.Auto
         };


        public static string Serialize(object toSerialize) => Serialize(toSerialize, DefaultSettings);

        public static string Serialize(object toSerialize, TypeInfoDecoration typeInfo) => Serialize(toSerialize, DefaultSettings, typeInfo);

        public static string Serialize(object toSerialize, JsonSerializerSettings settings,
            TypeInfoDecoration typeInfo = TypeInfoDecoration.None)
        {
            if (typeInfo == TypeInfoDecoration.JsonNet)
            {
                settings.TypeNameHandling = TypeNameHandling.All;
                return JsonConvert.SerializeObject(toSerialize, settings);
            }

            return JsonConvert.SerializeObject(toSerialize, settings);
        }

        private static object ToObject(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    return token.Children<JProperty>()
                        .ToDictionary(prop => prop.Name,
                            prop => ToObject(prop.Value));

                case JTokenType.Array:
                    return token.Select(ToObject).ToList();

                default:
                    return ((JValue)token).Value;
            }
        }

        public static object Deserialize(string json) =>
            ToObject(JsonConvert.DeserializeObject<JToken>(json,
                new JsonSerializerSettings { DateParseHandling = DateParseHandling.None }));
    }
}
