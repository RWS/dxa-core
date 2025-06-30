using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Sdl.Web.Common.Logging;

namespace Tridion.Dxa.Framework.Core
{
    public static class TypeLoader
    {
       // private static readonly string TYPE_CACHE = @"cil_types_cache_{0}.dat";
        private static readonly Type _ignoreAttribute = typeof(IgnoreTypeLoading);
        private static readonly List<Assembly> _assemblies;
        private static readonly Dictionary<string, string> _typeNames;
        private static readonly ConcurrentDictionary<string, Type> _types = new ConcurrentDictionary<string, Type>();

        static TypeLoader()
        {
            _assemblies = LoadAssemblies();
            _typeNames = new Dictionary<string, string>();
            LoadNonNativeTypes();
            LoadNativeTypes();
        }

        private static void LoadNativeTypes()
        {
            AddType(typeof(String), false);
            AddType(typeof(Byte), false);
            AddType(typeof(List<>), false);
            AddType(typeof(DateTime), false);
            AddType(typeof(int), false);
            AddType(typeof(bool), false);
            AddType(typeof(float), false);
            AddType(typeof(double), false);
            AddType(typeof(Dictionary<,>), false);
            AddType(typeof(HashSet<>), false);
            AddType(typeof(object), false);
        }

        private static void LoadNonNativeTypes()
        {
            foreach (Assembly assembly in _assemblies)
            {
                try
                {
                    Type[] types = assembly.GetTypes();
                    if (types != null)
                    {
                        foreach (Type type in types)
                        {
                            if (!IgnoreType(type))
                            {
                                AddType(type, true);
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    if (e.LoaderExceptions != null)
                    {
                        for (int i = 0; i < e.LoaderExceptions.Length; i++)
                        {
                            Log.Debug("Failed to load types from assembly: " + assembly.FullName, e.LoaderExceptions[i]);
                        }
                    }
                    else
                    {
                        Log.Debug("Failed to load types from assembly: " + assembly.FullName, e);
                    }
                }
                catch (Exception e)
                {
                    Log.Debug("Failed to load types from assembly: " + assembly.FullName, e);
                }
            }
        }

        private static Type AddType(Type type, bool getInterfaceMappings)
        {
            string assemblyQualifiedName = type.AssemblyQualifiedName;
            _typeNames[type.FullName] = assemblyQualifiedName;
            _typeNames[type.Name] = assemblyQualifiedName;
            _types.TryAdd(assemblyQualifiedName, type);
            if (getInterfaceMappings)
            {
                Type[] interfaces = type.GetInterfaces();
                if (interfaces != null)
                {
                    foreach (Type interfaceType in interfaces)
                    {
                        string interfaceName = "i:" + (interfaceType.FullName ?? interfaceType.Name);
                        string fullyQualifiedName = type.AssemblyQualifiedName;
                        _typeNames[interfaceName] = fullyQualifiedName;
                        _types.TryAdd(fullyQualifiedName, interfaceType);
                    }
                }
            }
            return type;
        }

        private static bool IgnoreType(Type type)
        {
            try
            {
                return type != null && Attribute.GetCustomAttribute(type, _ignoreAttribute) != null;
            }
            catch
            {
                return false;
            }
        }

        private static Type GetType(string fullyQualifiedTypeName)
        {
            return _types.GetOrAdd(fullyQualifiedTypeName, n => Type.GetType(n));
        }

        private static bool AllowedAssembly(Assembly assembly)
        {
            return assembly != null && AllowedAssembly(assembly.FullName);
        }

        private static bool AllowedAssembly(AssemblyName assembly)
        {
            return assembly != null && AllowedAssembly(assembly.FullName);
        }

        private static bool AllowedAssembly(string assemblyFullName)
        {
            return assemblyFullName != null &&
                (assemblyFullName.StartsWith("SDL", StringComparison.OrdinalIgnoreCase) ||
                assemblyFullName.StartsWith("DD4T", StringComparison.OrdinalIgnoreCase) ||
                assemblyFullName.StartsWith("Tridion", StringComparison.OrdinalIgnoreCase));
        }

        private static List<Assembly> LoadAssemblies()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => AllowedAssembly(x)).Select(x => x).ToList();
                assemblies.Add(Assembly.GetEntryAssembly());
                assemblies.Add(Assembly.GetCallingAssembly());
                foreach (var assembly in assemblies)
                {
                    LoadReferencedAssemblies(assembly);
                }
                return AppDomain.CurrentDomain.GetAssemblies().Where(x => AllowedAssembly(x)).Select(x => x).ToList();
            }
            catch (Exception e)
            {
                Log.Debug("Failed to load assemblies for type matching.", e);
            }
            return new List<Assembly>();
        }

        private static void LoadReferencedAssemblies(Assembly assembly)
        {
            if (assembly == null)
                return;
            try
            {
                AssemblyName[] assemblyNames = assembly.GetReferencedAssemblies();
                List<AssemblyName> referencedAssemblies = assemblyNames.Where(x => AllowedAssembly(x)).Select(y => y).ToList();
                if (referencedAssemblies.Count == 0)
                    return;
                Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (AssemblyName name in referencedAssemblies)
                {
                    if (!loadedAssemblies.Any(x =>
                        x.FullName.Equals(name.FullName, StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            LoadReferencedAssemblies(Assembly.Load(name));
                        }
                        catch (Exception e)
                        {
                            Log.Debug("Problem loading referenced assembly.", e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Debug("Problem loading referenced assembly.", e);
            }
        }

        public static Type Find(string typeName)
        {
            if (!string.IsNullOrEmpty(typeName))
            {
                if (_typeNames.ContainsKey(typeName))
                    return GetType(_typeNames[typeName]);

                if (typeName.EndsWith("[]"))
                {
                    typeName = typeName.Substring(0, typeName.Length - 2);
                    return ReflectionUtils.CreateArray(TypeLoader.Find(typeName), 1).GetType();
                }
            }
            return null;
        }

        public static Type Find(string typeName, string fullNamespace, string languageDependantNamespace)
        {
            return Find(typeName.Replace(fullNamespace, languageDependantNamespace));
        }

        public static Type FindConcreteType(Type actualType)
        {
            string name = actualType.FullName ?? actualType.Name;
            Type type = Find(name);
            if (type != null && (type.IsInterface || type.IsAbstract))
            {
                return GetType(_typeNames["i:" + name]);
            }
            return actualType;
        }
    }
}
