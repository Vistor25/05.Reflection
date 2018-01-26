using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace MyIoC
{
	public class Container
	{
        public Dictionary<Type, Dictionary<object, int[]>> exportTypes { get; set; }

        public List<Type> registeredTypes;

        private readonly Dictionary<Type, Func<object>> compiledCreators = new Dictionary<Type, Func<object>>();

        #region constructors
        public Container()
        {
            exportTypes = new Dictionary<Type, Dictionary<object, int[]>>();
            registeredTypes = new List<Type>();
        }

        public Container(Assembly assembly) : this()
        {
            this.AddAssembly(assembly);
        }

        public Container(IEnumerable<Assembly> assemblies) : this()
        {
            this.AddAssemblies(assemblies);
        }
        #endregion


        public void AddAssembly(Assembly assembly)
		{
            this.AddAssemblies(new List<Assembly>() { assembly });
        }

        public void AddAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null || assemblies?.Count() == 0)
            {
                throw new ArgumentException($"Argument cannot be null ar empty { nameof(assemblies) }");
            }

            List<Type> allTypes = assemblies.SelectMany(assembly => assembly.GetTypes().Where(type => type.GetCustomAttribute<ExportAttribute>(true) != null)).ToList();

            List<Type> typesWithContract = allTypes.Where(type => type.GetCustomAttribute<ExportAttribute>().Contract != null).ToList();

            if (typesWithContract?.Count() > 0)
            {
                this.RegisterTypes(false, typesWithContract);
            }

            if (allTypes?.Count() > 0)
            {
                this.RegisterTypes(false, allTypes);
            }
        }
        
        private void RegisterTypes(bool recursion, IEnumerable<Type> types)
        {
            if (types == null || types?.Count() == 0)
            {
                throw new ArgumentException($"Argument cannot be null or empty { nameof(types) }");
            }

            foreach(var type in types)
            {
                if (type == null)
                {
                    throw new ArgumentException($"Cannot register this type { nameof(type) }");
                }

                Dictionary<object, int[]> emptyDict;
                if (exportTypes.TryGetValue(type, out emptyDict))
                {
                    continue;
                }

                ConstructorInfo constructor = type.GetConstructors().FirstOrDefault(ctor => ctor.IsDefined(typeof(ImportConstructorAttribute), true));

                if (constructor == null)
                {
                    exportTypes.Add(type, new Dictionary<object, int[]>
                    {
                        {
                            type.GetConstructor(Type.EmptyTypes), null
                        }
                    });


                    PropertyInfo[] properties = type.GetProperties().Where(prop => prop.IsDefined(typeof(ImportAttribute), true)).ToArray();
                    FieldInfo[] fields = type.GetFields().Where(field => field.IsDefined(typeof(ImportAttribute), true)).ToArray();

                    List<Type> nestedTypes = properties.Select(prop => prop.PropertyType).Union(fields.Select(field => field.FieldType)).Except(registeredTypes).ToList();

                    if (fields.Any() || properties.Any())
                    {
                        IEnumerable<Type> tempTypes = registeredTypes.Union(nestedTypes);
                        registeredTypes = new List<Type>(tempTypes);

                        foreach (var propertyInfo in properties)
                        {
                            var typeProp = propertyInfo.PropertyType;
                            exportTypes[type].Add(propertyInfo, new[] { tempTypes.ToList().IndexOf(typeProp) });
                        }


                        foreach (var fieldInfo in fields)
                        {
                            var typeField = fieldInfo.FieldType;
                            exportTypes[type].Add(fieldInfo, new[] { tempTypes.ToList().IndexOf(typeField) });
                        }

                        if (nestedTypes.Except(types).Any())
                        {
                            RegisterTypes(true, nestedTypes);
                        }
                    }
                }
                else
                {
                    var parameters = constructor.GetParameters().Select(param => param.ParameterType).ToArray();

                    IEnumerable<Type> tempTypes = registeredTypes.Union(parameters);
                    registeredTypes = new List<Type>(tempTypes);

                    exportTypes.Add(type, new Dictionary<object, int[]>
                    {
                        {
                            constructor,
                            parameters.Select(item => tempTypes.ToList().IndexOf(item)).ToArray()
                        }
                    });

                    if (parameters.Except(types).Any())
                    {
                        RegisterTypes(true, parameters);
                    }
                }

                registeredTypes.Add(type);
                var customAttribute = type.GetCustomAttribute<ExportAttribute>();

                if (customAttribute == null)
                {
                    if (recursion)
                    {
                        registeredTypes.Remove(type);
                        exportTypes.Remove(type);
                    }
                }
                else
                {
                    if (customAttribute.Contract != null && !registeredTypes.Contains(customAttribute.Contract))
                    {
                        var tempList = registeredTypes.ToList();
                        var contract = customAttribute.Contract;
                        var value = exportTypes[type];

                        tempList[tempList.IndexOf(type)] = contract;
                        registeredTypes = new List<Type>(tempList);
                        exportTypes.Remove(type);
                        exportTypes.Add(contract, value);
                    }
                }
            }
        }

		public object CreateInstance(Type type)
		{
            Func<object> constructor;

            if (!compiledCreators.TryGetValue(type, out constructor))
            {
                ILGenerator methodBuilder = CreateMethod(type);
                constructor = methodBuilder.Compile();
                compiledCreators.Add(type, constructor);
            }

            return constructor();
        }

		public T CreateInstance<T>()
		{
            Func<object> constructor;
            Type type = typeof(T);

            if (!compiledCreators.TryGetValue(type, out constructor))
            {
                ILGenerator methodBuilder = CreateMethod(type);
                constructor = methodBuilder.Compile();
                compiledCreators.Add(type, constructor);
            }

            return (T)constructor();
        }

        private ILGenerator CreateMethod(Type type)
        {
            ILGenerator methodBuilder = null;

            var ctor = exportTypes[type].First();
            var parameters = ctor.Value;

            if (parameters == null)
            {
                methodBuilder = DynamicMethodBuilder.Init((ConstructorInfo)ctor.Key, type);

                if (exportTypes[type].Count > 1)
                {
                    foreach (var dependence in exportTypes[type].Skip(1))
                    {
                        var info = dependence.Key;
                        methodBuilder = methodBuilder.Dup();
                        methodBuilder = CreateDependence(methodBuilder, registeredTypes.ElementAt(dependence.Value[0]));

                        if (info is PropertyInfo)
                        {
                            methodBuilder = methodBuilder.AddProperty((PropertyInfo)info);
                        }

                        if (info is FieldInfo)
                        {
                            methodBuilder = methodBuilder.AddField((FieldInfo)info);
                        }
                    }
                }
            }
            else
            {
                foreach (var dependence in parameters)
                {
                    methodBuilder = CreateDependence(methodBuilder, registeredTypes.ElementAt(dependence), type);
                }

                methodBuilder = methodBuilder.AddCtor((ConstructorInfo)ctor.Key);
            }
            return methodBuilder;
        }

        private ILGenerator CreateDependence(ILGenerator methodBuilder, Type type, Type initType = null)
        {
            var instructions = exportTypes[type];
            var ctor = instructions.First();

            if (ctor.Value == null)
            {
                methodBuilder = methodBuilder.AddCtor((ConstructorInfo)ctor.Key, initType);

                if (exportTypes[type].Count > 1)
                {
                    foreach (var dependence in exportTypes[type].Skip(1))
                    {
                        var info = dependence.Key;
                        methodBuilder = methodBuilder.Dup();
                        methodBuilder = CreateDependence(methodBuilder, registeredTypes.ElementAt(dependence.Value[0]), initType);

                        if (info is PropertyInfo)
                        {
                            methodBuilder = methodBuilder.AddProperty((PropertyInfo)info);
                        }

                        if (info is FieldInfo)
                        {
                            methodBuilder = methodBuilder.AddField((FieldInfo)info);
                        }
                    }
                }
            }
            else
            {
                foreach (var dependence in ctor.Value)
                {
                    methodBuilder = CreateDependence(methodBuilder, registeredTypes.ElementAt(dependence));
                }

                methodBuilder = methodBuilder.AddCtor((ConstructorInfo)ctor.Key);
            }

            return methodBuilder;
        }

    }
}
