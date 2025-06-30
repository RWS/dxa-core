using System.Collections.Generic;
using Tridion.Dxa.Api.Client.GraphQLClient.Schema;
using Tridion.Dxa.Api.Client.Utils;

namespace Tridion.Dxa.Api.Client.CodeGen
{
    public static class GraphQLSchemaExt
    {
        public static string TypeName(this GraphQLSchemaInterface interfaceType)
            => $"I{interfaceType.Name.PascalCase()}";

        public static string TypeName(this GraphQLSchemaType type)
        {
            switch (type.Kind)
            {
                case "OBJECT":
                case "INPUT_OBJECT":
                case "ENUM":
                    return $"{type.Name.PascalCase()}";
                case "INTERFACE":
                    return $"I{type.Name.PascalCase()}";
                default:
                    return null;
            }
        }

        public static bool IsObjectType(this GraphQLSchemaType type)
            => type.Kind == "OBJECT";

        public static bool IsInterfaceType(this GraphQLSchemaType type)
            => type.Kind == "INTERFACE";

        public static List<GraphQLSchemaTypeInfo> ConcreteTypes(this GraphQLSchemaType type) 
            => type.PossibleTypes ?? new List<GraphQLSchemaTypeInfo>();

        public static bool IsObjectType(this GraphQLSchemaField field)
        {
            if (field.Type.Kind == "OBJECT") return true;
            return field.Type.OfType != null && field.Type.OfType.Kind.Equals("OBJECT");
        }

        public static bool IsInterfaceType(this GraphQLSchemaField field)
        {
            if (field.Type.Kind.Equals("INTERFACE")) return true;
            return field.Type.OfType != null && field.Type.OfType.Kind.Equals("INTERFACE");
        }

        public static bool HasArguments(this GraphQLSchemaField field)
            => field.Args != null && field.Args.Count > 0;

        public static string GetFieldTypeName(this GraphQLSchemaField field) 
            => field.Type.OfType != null ? GetFieldTypeName(field.Type) : field.Type.Name;

        public static string GetFieldTypeName(this GraphQLSchemaTypeInfo typeInfo) 
            => typeInfo.OfType != null ? typeInfo.OfType.GetFieldTypeName() : typeInfo.Name;

        public static string EmitTypeDecl(this GraphQLSchemaType type)
        {
            switch (type.Kind)
            {
                case "OBJECT":
                case "INPUT_OBJECT":
                    return $"partial class {TypeName(type)}";
                case "INTERFACE":
                    return $"interface {TypeName(type)}";
                case "ENUM":
                    return $"enum {TypeName(type)}";
                default:
                    return null;
            }
        }

        public static string EmitTypeDecl(this GraphQLSchemaField field)
            => TypeName(field.Type);

        public static string TypeName(this GraphQLSchemaTypeInfo typeInfo, bool nullable = true)
        {
            if (typeInfo.Name == null && typeInfo.OfType != null)
            {
                switch (typeInfo.Kind)
                {
                    case "LIST":
                        return $"List<{TypeName(typeInfo.OfType)}>";
                    case "Map":
                        return $"IDictionary<{TypeName(typeInfo.OfType)}>";
                    case "String":
                        return $"{TypeName(typeInfo.OfType)}";
                    default:
                        return $"{TypeName(typeInfo.OfType, false)}{(typeInfo.Kind.Equals("NON_NULL") ? "" : "?")}";
                }
            }
            switch (typeInfo.Kind)
            {
                case "SCALAR":
                    switch (typeInfo.Name)
                    {
                        case "ID":
                            return "string";
                        case "Map":
                            return "IDictionary";
                        case "String":
                            return "string";
                        case "Boolean":
                            return "bool" + (nullable ? "?" : "");
                        case "Int":
                            return "int" + (nullable ? "?" : "");
                        default:
                            return typeInfo.Name.ToLower();
                    }
                case "LIST":
                    return $"List<{TypeName(typeInfo.OfType)}>";
                case "Map":
                    return $"IDictionary<{TypeName(typeInfo.OfType)}>";
                case "OBJECT":
                case "INPUT_OBJECT":
                    return $"{typeInfo.Name}";
                case "INTERFACE":
                    return $"I{typeInfo.Name.PascalCase()}";
                case "ENUM":
                    return $"{typeInfo.Name.PascalCase()}";
                default:
                    return "<Unknown Type>";
            }
        }
    }
}
