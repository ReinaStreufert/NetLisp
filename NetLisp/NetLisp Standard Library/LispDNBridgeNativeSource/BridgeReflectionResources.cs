using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LispDNBridgeNativeSource
{
    static class BridgeReflectionResources
    {
        private static Type[] allTypes;

        // used for merging overload method entries
        private static Dictionary<string, MethodBridgeSymbol> methodDictionary = new Dictionary<string, MethodBridgeSymbol>();
        // used for skipping duplicate field and property names (of which .net reflection seems to be full of?)
        private static HashSet<string> definedNames = new HashSet<string>();
        // used for optimization. if a namespace's symbols have already been loaded once they are saved and can be retreived again
        // when other non-communicating modules request them.
        private static Dictionary<string, BridgeSymbol[]> namespaceCache = new Dictionary<string, BridgeSymbol[]>();

        public static void Initialize()
        {
            allTypes = AppDomain.CurrentDomain.GetAssemblies()
                       .SelectMany(t => t.GetExportedTypes()).ToArray();
        }

        public static IEnumerable<BridgeSymbol> GenerateBridgeSymbols(string[] namespaces)
        {
            List<string> typesearchNamespaces = new List<string>();

            foreach (string requestedNamespace in namespaces)
            {
                if (namespaceCache.ContainsKey(requestedNamespace))
                {
                    foreach (BridgeSymbol bridgeSymbol in namespaceCache[requestedNamespace])
                    {
                        yield return bridgeSymbol;
                    }
                } else
                {
                    typesearchNamespaces.Add(requestedNamespace);
                }
            }

            List<BridgeSymbol>[] namespaceSymbols = new List<BridgeSymbol>[typesearchNamespaces.Count];
            for (int i = 0; i < namespaceSymbols.Length; i++)
            {
                namespaceSymbols[i] = new List<BridgeSymbol>();
            }
            foreach (Type type in allTypes)
            {
                if (type.IsSpecialName) continue;
                int namespaceIndex = typesearchNamespaces.IndexOf(type.Namespace);
                if (namespaceIndex < 0) continue;
                List<BridgeSymbol> cacheList = namespaceSymbols[namespaceIndex];
                foreach (BridgeSymbol bridgeSymbol in iterateBridgeSymbolsForType(typesearchNamespaces, type))
                {
                    cacheList.Add(bridgeSymbol);
                    yield return bridgeSymbol;
                }
            }
        }

        public static BridgeSymbol[] GenerateBridgeSymbols(Type type, string baseName)
        {
            return iterateBridgeSymbolsForType(baseName, type).ToArray();
        }

        public static void LoadAsm(string assemblyName)
        {
            Assembly loadedAsm = Assembly.Load(assemblyName);
            allTypes = allTypes.Concat(loadedAsm.GetExportedTypes()).ToArray();
        }

        private static IEnumerable<BridgeSymbol> iterateBridgeSymbolsForType(IEnumerable<string> namespaces, Type type)
        {
            return iterateBridgeSymbolsForType(localizeName(type.FullName, namespaces), type);
        }

        private static IEnumerable<BridgeSymbol> iterateBridgeSymbolsForType(string defName, Type type)
        {
            const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            string nameDot = defName + ".";
            string nameColon = defName + ":";
            yield return new TypeBridgeSymbol(defName, type);
            if (type.IsEnum)
            {
                Array enumValues = type.GetEnumValues();
                string[] enumNames = type.GetEnumNames();
                for (int i = 0; i < enumValues.Length; i++)
                {
                    yield return new EnumBridgeSymbol(nameDot + enumNames[i], enumValues.GetValue(i));
                }
            } else
            {
                yield return new ConstructorBridgeSymbol(nameDot + "new", type.GetConstructors());
                foreach (FieldInfo field in type.GetFields(bindingFlags))
                {
                    string name;
                    if (field.IsStatic)
                    {
                        name = nameColon + field.Name;
                    } else
                    {
                        name = nameDot + field.Name;
                    }
                    if (definedNames.Contains(name))
                    {
                        continue;
                    }
                    yield return new FieldBridgeSymbol(name, field);
                    definedNames.Add(name);
                }
                foreach (PropertyInfo property in type.GetProperties(bindingFlags))
                {
                    string name;
                    if (property.IsStatic())
                    {
                        name = nameColon + property.Name;
                    } else
                    {
                        name = nameDot + property.Name;
                    }
                    if (definedNames.Contains(name))
                    {
                        continue;
                    }
                    yield return new PropertyBridgeSymbol(name, property);
                    definedNames.Add(name);
                }
            }
            foreach (MethodInfo method in type.GetMethods(bindingFlags))
            {
                string methodName = method.Name;
                // conversion operator overloads cannot be differed by parameter types, so we edit their
                // already automatically given names to include return type. that way they are seperate
                // functions, not overloads
                if (methodName == "op_Implicit" || methodName == "op_Explicit")
                {
                    methodName = methodName + "." + method.ReturnType.Name;
                }
                string name;
                if (method.IsStatic)
                {
                    name = nameColon + methodName;
                } else
                {
                    name = nameDot + methodName;
                }
                if (definedNames.Contains(name))
                {
                    name = name + "_m"; // rarely methods may share names with properties and fields
                }
                if (methodDictionary.ContainsKey(name))
                {
                    methodDictionary[name].AddOverload(method);
                }
                else
                {
                    MethodBridgeSymbol methodBridgeSymbol = new MethodBridgeSymbol(name, method);
                    methodDictionary.Add(name, methodBridgeSymbol);
                    yield return methodBridgeSymbol;
                }
            }
            methodDictionary.Clear();
            definedNames.Clear();
        }

        private static string localizeName(string name, IEnumerable<string> namespaces)
        {
            int largestMatch = -1;
            foreach (string namespaceName in namespaces)
            {
                if (namespaceName.Length > largestMatch && name.StartsWith(namespaceName))
                {
                    largestMatch = namespaceName.Length;
                }
            }
            if (largestMatch > 0)
            {
                return name.Substring(largestMatch + 1); // skip dot
            } else
            {
                return name;
            }
        }
    }
    abstract class BridgeSymbol
    {
        public string SymbolName { get; set; }
        public abstract BridgeType BridgeType { get; }
        public abstract LispToken CreateBridgeDefinition();
    }
    class TypeBridgeSymbol : BridgeSymbol
    {
        public override BridgeType BridgeType => BridgeType.Type;

        public Type RepresentingType { get; set; }
        public TypeBridgeSymbol(string symbolName, Type representingType)
        {
            SymbolName = symbolName;
            RepresentingType = representingType;
        }

        private DotnetType cachedDefinition = null;

        public override LispToken CreateBridgeDefinition()
        {
            if (cachedDefinition == null)
            {
                cachedDefinition = new DotnetType(RepresentingType);
            }
            return cachedDefinition;
        }
    }
    class FieldBridgeSymbol : BridgeSymbol
    {
        public override BridgeType BridgeType => BridgeType.Field;

        public FieldInfo RepresentingField { get; set; }
        public FieldBridgeSymbol(string symbolName, FieldInfo representingField)
        {
            SymbolName = symbolName;
            RepresentingField = representingField;
        }

        private FieldBridge cachedDefinition = null;

        public override LispToken CreateBridgeDefinition()
        {
            if (cachedDefinition == null)
            {
                cachedDefinition = new FieldBridge(RepresentingField);
            }
            return cachedDefinition;
        }
    }
    class PropertyBridgeSymbol : BridgeSymbol
    {
        public override BridgeType BridgeType => BridgeType.Property;

        public PropertyInfo RepresentingProperty { get; set; }
        public PropertyBridgeSymbol(string symbolName, PropertyInfo representingProperty)
        {
            SymbolName = symbolName;
            RepresentingProperty = representingProperty;
        }

        private PropertyBridge cachedDefinition = null;

        public override LispToken CreateBridgeDefinition()
        {
            if (cachedDefinition == null)
            {
                cachedDefinition = new PropertyBridge(RepresentingProperty);
            }
            return cachedDefinition;
        }
    }
    class MethodBridgeSymbol : BridgeSymbol
    {
        public override BridgeType BridgeType => BridgeType.Method;
        public List<MethodInfo> RepresentingMethodGroup { get; set; }

        private MethodGroupBridge cachedDefinition = null;

        public MethodBridgeSymbol(string symbolName, params MethodInfo[] representingMethodGroup)
        {
            SymbolName = symbolName;
            RepresentingMethodGroup = representingMethodGroup.ToList();
        }

        public void AddOverload(MethodInfo method)
        {
            if (cachedDefinition == null)
            {
                RepresentingMethodGroup.Add(method);
            } else
            {
                cachedDefinition.AddOverload(method);
            }
        }

        public override LispToken CreateBridgeDefinition()
        {
            if (cachedDefinition == null)
            {
                cachedDefinition = new MethodGroupBridge(RepresentingMethodGroup);
            }
            return cachedDefinition;
        }
    }
    class ConstructorBridgeSymbol : BridgeSymbol
    {
        public override BridgeType BridgeType => BridgeType.Constructor;

        public ConstructorInfo[] RepresentingConstructorGroup;
        public ConstructorBridgeSymbol(string symbolName, ConstructorInfo[] constructors)
        {
            SymbolName = symbolName;
            RepresentingConstructorGroup = constructors;
        }

        private ConstructorBridge cachedDefinition = null;

        public override LispToken CreateBridgeDefinition()
        {
            if (cachedDefinition == null)
            {
                cachedDefinition = new ConstructorBridge(RepresentingConstructorGroup);
            }
            return cachedDefinition;
        }
    }
    class EnumBridgeSymbol : BridgeSymbol
    {
        public override BridgeType BridgeType => BridgeType.EnumerationValue;

        public object EnumValue { get; set; }
        public EnumBridgeSymbol(string symbolName, object enumValue)
        {
            SymbolName = symbolName;
            EnumValue = enumValue;
        }

        private LispToken cachedCastResult = null;

        public override LispToken CreateBridgeDefinition()
        {
            if (cachedCastResult == null)
            {
                LispToken castResult;
                if (BridgeCaster.TryCast(EnumValue, out castResult))
                {
                    cachedCastResult = castResult;
                }
                else
                {
                    cachedCastResult = new DotnetInstance(EnumValue);
                }
            }
            return cachedCastResult;
        }
    }

    enum BridgeType
    {
        Type,
        Field,
        Property,
        Method,
        Constructor,
        EnumerationValue
    }
    public static class PropertyInfoExtensions
    {
        public static bool IsStatic(this PropertyInfo source, bool nonPublic = false)
            => source.GetAccessors(nonPublic).Any(x => x.IsStatic);
    }
}
