using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Core
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class IgnoreTypeLoading : Attribute
    {
    }

    /// <summary>
    /// ReflectionUtils
    /// 
    /// A collection of useful functions for dealing with .NET reflection
    /// </summary>
    public class ReflectionUtils
    {
        private static readonly object[] EmptyObjects = new object[] { };
        public delegate object GetDelegate(object source);
        public delegate void SetDelegate(object source, object value);
        public delegate object ConstructorDelegate(params object[] args);
        public delegate TValue ThreadSafeDictionaryValueFactory<TKey, TValue>(TKey key);

        /// <summary>
        /// Create instance of a type.
        /// </summary>
        /// <param name="type">Type to create.</param>
        /// <returns></returns>
        public static object CreateInstance(Type type, object[] constructorArgs = null)
        {
            return Activator.CreateInstance(
                            TypeLoader.FindConcreteType(type),
                            BindingFlags.NonPublic |
                            BindingFlags.Public |
                            BindingFlags.Instance |
                            BindingFlags.OptionalParamBinding,
                            null, constructorArgs, null);
        }

        public static Type CreateGenericListType(Type type) => typeof(List<>).MakeGenericType(type);

        public static IList CreateGenericListInstance(Type type) => (IList)Activator.CreateInstance(CreateGenericListType(type));

        public static Type CreateGenericDictionaryType(Type keyType, Type valueType) => typeof(Dictionary<,>).MakeGenericType(keyType, valueType);

        public static IDictionary CreateGenericDictionaryInstance(Type keyType, Type valueType) => (IDictionary)Activator.CreateInstance(CreateGenericDictionaryType(keyType, valueType));

        public static Array CreateArray(Type type, int count) => Array.CreateInstance(type, count);

        public static TypeInfo GetTypeInfo(Type type) => type.GetTypeInfo();

        public static Type GetGenericListElementType(Type type)
        {
            IEnumerable<Type> interfaces;
            interfaces = type.GetTypeInfo().ImplementedInterfaces;
            foreach (Type implementedInterface in interfaces)
            {
                if (IsTypeGeneric(implementedInterface) &&
                    implementedInterface.GetGenericTypeDefinition() == typeof(IList<>))
                {
                    return GetGenericTypeArguments(implementedInterface)[0];
                }
            }
            return GetGenericTypeArguments(type)[0];
        }

        public static Type[] GetGenericTypeArguments(Type type) => type.GetTypeInfo().GenericTypeArguments;

        public static bool IsTypeGeneric(Type type) => GetTypeInfo(type).IsGenericType;

        public static bool IsTypeGenericeCollectionInterface(Type type)
        {
            if (!IsTypeGeneric(type))
                return false;

            Type genericDefinition = type.GetGenericTypeDefinition();

            return (genericDefinition == typeof(IList<>)
                || genericDefinition == typeof(ICollection<>)
                || genericDefinition == typeof(IEnumerable<>)
                // || genericDefinition == typeof(IReadOnlyCollection<>)
                // || genericDefinition == typeof(IReadOnlyList<>)
                );
        }

        public static bool IsAssignableFrom(Type type1, Type type2) => GetTypeInfo(type1).IsAssignableFrom(GetTypeInfo(type2));

        public static bool IsTypeGenericList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

        public static bool IsTypeDictionary(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return true;

            if (typeof(IDictionary<,>).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
                return true;

            if (!GetTypeInfo(type).IsGenericType)
                return false;

            Type genericDefinition = type.GetGenericTypeDefinition();
            return genericDefinition == typeof(IDictionary<,>);
        }

        public static bool IsTypeHashSet(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>);

        public static bool IsNullableType(Type type) => GetTypeInfo(type).IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        public static object ToNullableType(object obj, Type nullableType) => obj == null ? null : Convert.ChangeType(obj, Nullable.GetUnderlyingType(nullableType), CultureInfo.InvariantCulture);

        public static bool IsValueType(Type type) => GetTypeInfo(type).IsValueType;

        public static IEnumerable<ConstructorInfo> GetConstructors(Type type) => type.GetTypeInfo().DeclaredConstructors;

        public static ConstructorInfo GetConstructorInfo(Type type, params Type[] argsType)
        {
            IEnumerable<ConstructorInfo> constructorInfos = GetConstructors(type);
            int i;
            bool matches;
            foreach (ConstructorInfo constructorInfo in constructorInfos)
            {
                ParameterInfo[] parameters = constructorInfo.GetParameters();
                if (argsType.Length != parameters.Length)
                    continue;

                i = 0;
                matches = true;
                foreach (ParameterInfo parameterInfo in constructorInfo.GetParameters())
                {
                    if (parameterInfo.ParameterType != argsType[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return constructorInfo;
            }

            return null;
        }

        public static IEnumerable<PropertyInfo> GetProperties(Type type) => type.GetProperties();

        public static IEnumerable<FieldInfo> GetFields(Type type) => type.GetFields();

        public static MethodInfo GetGetterMethodInfo(PropertyInfo propertyInfo) => propertyInfo.GetGetMethod();

        public static MethodInfo GetSetterMethodInfo(PropertyInfo propertyInfo) => propertyInfo.GetSetMethod(true);

        public static ConstructorDelegate GetContructor(ConstructorInfo constructorInfo) => GetConstructorByReflection(constructorInfo);

        public static ConstructorDelegate GetContructor(Type type, params Type[] argsType) => GetConstructorByReflection(type, argsType);

        public static ConstructorDelegate GetConstructorByReflection(ConstructorInfo constructorInfo) => delegate (object[] args) { return constructorInfo.Invoke(args); };

        public static ConstructorDelegate GetConstructorByReflection(Type type, params Type[] argsType)
        {
            ConstructorInfo constructorInfo = GetConstructorInfo(type, argsType);
            return constructorInfo == null ? null : GetConstructorByReflection(constructorInfo);
        }

        public static GetDelegate GetGetMethod(PropertyInfo propertyInfo) => GetGetMethodByReflection(propertyInfo);

        public static GetDelegate GetGetMethod(FieldInfo fieldInfo) => GetGetMethodByReflection(fieldInfo);

        public static GetDelegate GetGetMethodByReflection(PropertyInfo propertyInfo)
        {
            MethodInfo methodInfo = GetGetterMethodInfo(propertyInfo);
            return delegate (object source) { return methodInfo.Invoke(source, EmptyObjects); };
        }

        public static GetDelegate GetGetMethodByReflection(FieldInfo fieldInfo) => delegate (object source) { return fieldInfo.GetValue(source); };

        public static SetDelegate GetSetMethod(PropertyInfo propertyInfo) => GetSetMethodByReflection(propertyInfo);

        public static SetDelegate GetSetMethod(FieldInfo fieldInfo) => GetSetMethodByReflection(fieldInfo);

        public static SetDelegate GetSetMethodByReflection(PropertyInfo propertyInfo)
        {
            MethodInfo methodInfo = GetSetterMethodInfo(propertyInfo);
            return delegate (object source, object value)
            {
                try
                {
                    methodInfo.Invoke(source, new object[] { value });
                }
                catch
                {
                    // failed to set value -- ignore
                }
            };
        }

        public static SetDelegate GetSetMethodByReflection(FieldInfo fieldInfo)
        {
            return delegate (object source, object value)
            {
                try
                {
                    fieldInfo.SetValue(source, value);
                }
                catch
                {
                    // failed to set value -- ignore
                }
            };
        }

        public static bool HasAttribute(object obj, string propertyName, Type attributeType) => HasAttribute(obj.GetType(), propertyName, attributeType);

        public static bool HasAttribute(Type type, string propertyName, Type attributeType)
        {
            try
            {
                var pi = type.GetProperty(propertyName);
                return pi != null && Attribute.IsDefined(pi, attributeType);
            }
            catch
            {
                return false;
            }
        }

        public static bool HasAttribute(Type type, Type attributeType)
        {
            try
            {
                return type != null && attributeType != null && Attribute.GetCustomAttribute(type, attributeType) != null;
            }
            catch
            {
                return false;
            }
        }

        public static Version AssemblyVersion => Assembly.GetCallingAssembly().GetName().Version;

        public static Version AssemblyFileVersion
        {
            get
            {
                string v = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
                Version version;
                if (Version.TryParse(v, out version))
                {
                    return version;
                }
                else
                {
                    return null;
                }
            }
        }

        public static List<T> CollectAttributesFromCallStack<T>() where T : Attribute
        {
            List<T> attribs = new List<T>();
            StackTrace stack = new StackTrace();
            StackFrame[] frames = stack.GetFrames();
            foreach (StackFrame frame in frames)
            {
                T v = frame.GetMethod().GetCustomAttribute<T>();
                if (v != null)
                {
                    attribs.Add(v);
                }
            }
            return attribs;
        }
    }
}
