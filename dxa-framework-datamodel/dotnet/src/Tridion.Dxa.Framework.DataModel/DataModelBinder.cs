using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace Sdl.Web.DataModel
{
    /// <summary>
    /// Serialization Binder which supports polymorphic deserialization of Data Model objects using JSON.NET.
    /// </summary>
    /// <remarks>
    /// Class <see cref="ContentModelData"/> has loosely typed values. In order to ensure that the appropriate types are deserialized,
    /// some type information has to be included in the serialized JSON.
    /// This is done using JSON.NET's <see cref="TypeNameHandling.Auto"/> feature, in combination with this <see cref="DataModelBinder"/>.
    /// This results in <c>$type</c> metadata properties in the JSON with (unqualified) type names of the Data Model types.
    /// </remarks>
    public class DataModelBinder : ISerializationBinder
    {
        /// <summary>
        /// JSON.NET Serializer Settings to be used to polymorphically (de-)serialize Data Models.
        /// </summary>
        public static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new DataModelBinder(),
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        #region SerializationBinder Overrides
        /// <summary>
        /// Obtains the type name (and optional assembly) name to include as <c>$type</c> metadata in the JSON.
        /// </summary>
        /// <param name="serializedType">The serialized type.</param>
        /// <param name="assemblyName">The assembly name. If <c>null</c>, no assembly name is included.</param>
        /// <param name="typeName">The type name to to include as <c>$type</c> metadata in the JSON.</param>
        public virtual void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = serializedType.Name;
        }

        /// <summary>
        /// Obtains the type to deserialize into based on the <c>$type</c> metadata in the JSON.
        /// </summary>
        /// <param name="assemblyName">The assembly name obtained from the <c>$type</c> metadata in the JSON.</param>
        /// <param name="typeName">The type name obtained from the <c>$type</c> metadata in the JSON.</param>
        /// <returns></returns>
        public virtual Type BindToType(string assemblyName, string typeName)
        {
            // Unfortunately, type System.Float does not exist (it's called System.Single), hence we have special handling here
            if (typeName.StartsWith("Float"))
            {
                // Note: Switch from Float to Double so deserialization of json produces doubles instead of floats to
                // help prevent potential upcasts of floats to doubles later that may produce extra noise.
                typeName = typeName.Replace("Float", "Double");
            }

            return Type.GetType($"Sdl.Web.DataModel.{typeName}") ?? Type.GetType($"System.{typeName}", throwOnError: true);
        }
        #endregion
    }
}
