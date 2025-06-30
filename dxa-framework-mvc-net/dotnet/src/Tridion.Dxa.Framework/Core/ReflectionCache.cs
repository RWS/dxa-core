using Sdl.Web.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Core
{
    public class CreateObjectEventArgs
    {
        public Type ObjectType { get; protected set; }
        public object CreatedObject { get; set; }
        public CreateObjectEventArgs(Type type)
        {
            ObjectType = type;
        }
    }

    /// <summary>
    /// ReflectionCache
    /// </summary>
    public class ReflectionCache
    {
        private static volatile ReflectionCache _instance;
        private static object _syncRoot = new Object();

        private static readonly Type[] _emptyTypes = new Type[0];
        private static readonly object[] _emptyObjects = new object[0];
        private static readonly Type[] _arrayConstructorParameterTypes = new Type[] { typeof(int) };

        private readonly ConcurrentDictionary<Type, ConstructorInfo> _constructors;
        private readonly ConcurrentDictionary<Type, IDictionary<string, ReflectionUtils.GetDelegate>> _getters;
        private readonly ConcurrentDictionary<Type, IDictionary<string, KeyValuePair<Type, ReflectionUtils.SetDelegate>>> _setters;

        protected event EventHandler<CreateObjectEventArgs> _createObject;


        protected ReflectionCache()
        {
            _constructors = new ConcurrentDictionary<Type, ConstructorInfo>();
            _getters = new ConcurrentDictionary<Type, IDictionary<string, ReflectionUtils.GetDelegate>>();
            _setters = new ConcurrentDictionary<Type, IDictionary<string, KeyValuePair<Type, ReflectionUtils.SetDelegate>>>();
        }

        public static ReflectionCache Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null)
                            _instance = new ReflectionCache();
                    }
                }

                return _instance;
            }
        }

        public event EventHandler<CreateObjectEventArgs> OnCreateObject
        {
            add
            {
                _createObject += value;
            }
            remove
            {
                _createObject -= value;
            }
        }

        /// <summary>
        /// Create instance of a type
        /// </summary>
        /// <param name="type">Type of instance to create</param>
        /// <returns>Instance</returns>
        public object CreateInstance(Type type)
        {
            try
            {
                if (_createObject != null)
                {
                    Type concreateType = TypeLoader.FindConcreteType(type);
                    CreateObjectEventArgs args = new CreateObjectEventArgs(
                        concreateType
                        );
                    _createObject(this, args);
                    if (args.CreatedObject != null) return args.CreatedObject;
                }
                return ReflectionUtils.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Log.Debug("Failed to create instance using ReflectionUtils.CreateInstance...", ex);
            }
            Log.Debug("CreateInstance(Type type) returns null");
            return null;
        }

        /// <summary>
        /// Returns a list of all property names of an object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public List<string> GetPropertyNames(object obj)
        {
            return GetPropertyValueGetters(obj.GetType()).Keys.ToList();
        }

        /// <summary>
        /// Returns property value by name of the specified object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public object GetPropertyValueFromObject(object obj, string propertyName)
        {
            return GetPropertyValueGetters(obj.GetType())[propertyName](obj);
        }

        /// <summary>
        /// Set a property value on the provided object by property name.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="propertyName"></param>
        /// <param name="val"></param>
        public void SetPropertyValue(object obj, string propertyName, object val)
        {
            var setters = GetPropertyValueSetters(obj.GetType());
            if (setters != null && setters.ContainsKey(propertyName))
            {
                setters[propertyName].Value(obj, val);
            }
        }

        public Type GetPropertyType(object obj, string propertyName)
        {
            var setters = GetPropertyValueSetters(obj.GetType());
            if (setters != null && setters.ContainsKey(propertyName))
            {
                return setters[propertyName].Key;
            }
            return null;
        }

        private ConstructorInfo GetConstructorInfo(Type type)
        {
            return _constructors.GetOrAdd(type, v =>
            {
                return ReflectionUtils.GetConstructorInfo(TypeLoader.FindConcreteType(type));
            });
        }

        private IDictionary<string, ReflectionUtils.GetDelegate> GetPropertyValueGetters(Type type)
        {
            return _getters.GetOrAdd(type, v =>
            {
                IDictionary<string, ReflectionUtils.GetDelegate> result = new Dictionary<string, ReflectionUtils.GetDelegate>();
                foreach (PropertyInfo propertyInfo in ReflectionUtils.GetProperties(type))
                {
                    if (propertyInfo.CanRead)
                    {
                        MethodInfo getMethod = ReflectionUtils.GetGetterMethodInfo(propertyInfo);
                        if (getMethod.IsStatic || !getMethod.IsPublic)
                            continue;
                        result[propertyInfo.Name] = ReflectionUtils.GetGetMethod(propertyInfo);
                    }
                }
                foreach (FieldInfo fieldInfo in ReflectionUtils.GetFields(type))
                {
                    if (fieldInfo.IsStatic || !fieldInfo.IsPublic)
                        continue;
                    result[fieldInfo.Name] = ReflectionUtils.GetGetMethod(fieldInfo);
                }
                return result;
            });
        }

        private IDictionary<string, KeyValuePair<Type, ReflectionUtils.SetDelegate>> GetPropertyValueSetters(Type type)
        {
            return _setters.GetOrAdd(type, v =>
            {
                IDictionary<string, KeyValuePair<Type, ReflectionUtils.SetDelegate>> result = new Dictionary<string, KeyValuePair<Type, ReflectionUtils.SetDelegate>>();
                foreach (PropertyInfo propertyInfo in ReflectionUtils.GetProperties(type))
                {
                    if (propertyInfo.CanWrite)
                    {
                        MethodInfo setMethod = ReflectionUtils.GetSetterMethodInfo(propertyInfo);
                        if (setMethod == null || setMethod.IsStatic)
                            continue;
                        result[propertyInfo.Name] = new KeyValuePair<Type, ReflectionUtils.SetDelegate>(propertyInfo.PropertyType, ReflectionUtils.GetSetMethod(propertyInfo));
                    }
                }
                foreach (FieldInfo fieldInfo in ReflectionUtils.GetFields(type))
                {
                    if (fieldInfo.IsInitOnly || fieldInfo.IsStatic || !fieldInfo.IsPublic)
                        continue;
                    result[fieldInfo.Name] = new KeyValuePair<Type, ReflectionUtils.SetDelegate>(fieldInfo.FieldType, ReflectionUtils.GetSetMethod(fieldInfo));
                }
                return result;
            });
        }
    }
}
