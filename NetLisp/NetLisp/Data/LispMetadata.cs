using NetLisp.Runtime;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public abstract class LispMetadata
    {
        public List<MetadataEntry> Entries { get; private set; } = new List<MetadataEntry>();

        public GeneralAnnotationMetadataEntry GeneralAnnotation
        {
            get
            {
                MetadataEntry? queryResult = Entries.Where((MetadataEntry entry) => entry.Type == MetadataEntryType.GeneralAnnotation).FirstOrDefault((MetadataEntry)null);
                if (queryResult == null)
                {
                    return GeneralAnnotationMetadataEntry.Default;
                } else
                {
                    return (GeneralAnnotationMetadataEntry)queryResult;
                }
            }
        }
    }
    public class ExecutableTokenMetadata : LispMetadata
    {
        public ExecutableTokenMetadata() { }
        public static ExecutableTokenMetadata Blank { get; } = new ExecutableTokenMetadata();
        public ExecutableTokenMetadata(ReturnMetadataEntry returnParam)
        {
            Entries.Add(returnParam);
        }

        public virtual bool HasDefinedArguments { get => false; }

        public ReturnMetadataEntry ReturnParam
        {
            get
            {
                MetadataEntry? queryResult = Entries.Where((MetadataEntry entry) => entry.Type == MetadataEntryType.Return).FirstOrDefault((MetadataEntry)null);
                if (queryResult == null)
                {
                    return ReturnMetadataEntry.Default;
                }
                else
                {
                    return (ReturnMetadataEntry)queryResult;
                }
            }
        }
        public AnyArgumentMetadataEntry AnyArgument
        {
            get
            {
                MetadataEntry? queryResult = Entries.Where((MetadataEntry entry) => entry.Type == MetadataEntryType.AnyArgument).FirstOrDefault((MetadataEntry)null);
                if (queryResult == null)
                {
                    return AnyArgumentMetadataEntry.Default;
                }
                else
                {
                    return (AnyArgumentMetadataEntry)queryResult;
                }
            }
        }
    }
    public class ArgumentDefinedMetadata : ExecutableTokenMetadata
    {
        public static List<MetadataParser> Parsers = new List<MetadataParser>();
        static ArgumentDefinedMetadata()
        {
            Parsers.Add(new GeneralAnnotationParser());
            Parsers.Add(new ReturnParamParser());
            Parsers.Add(new ArgumentParser());
        }
        private ArgumentDefinedMetadata() { }

        public override bool HasDefinedArguments => true;

        public static ArgumentDefinedMetadata CreateBlank(LispSymbol[] args)
        {
            return new ArgumentDefinedMetadata() { definedArguments = args };
        }
        public static bool TryParse(LispSymbol[] args, LispList metadataBody, out ArgumentDefinedMetadata metadata)
        {
            return parseInternal(null, args, metadataBody, out metadata);
        }
        public static ArgumentDefinedMetadata Parse(RuntimeContext runtimeContext, LispSymbol[] args, LispList metadataBody)
        {
            ArgumentDefinedMetadata result;
            parseInternal(runtimeContext, args, metadataBody, out result);
            return result;
        }
        private static bool parseInternal(RuntimeContext? runtimeContext, LispSymbol[] args, LispList metadataBody, out ArgumentDefinedMetadata metadata)
        {
            HashSet<MetadataParser> usedParsers = new HashSet<MetadataParser>();
            RuntimeErrorEvent runtimeErrorHandler = null;
            if (runtimeContext != null)
            {
                runtimeErrorHandler = (RuntimeError err) =>
                {
                    foreach (MetadataParser parser in usedParsers)
                    {
                        parser.Reset();
                    }
                    runtimeContext.RuntimeError -= runtimeErrorHandler;
                };
                runtimeContext.RuntimeError += runtimeErrorHandler;
            }
            ArgumentDefinedMetadata metadataResult = new ArgumentDefinedMetadata();
            metadataResult.definedArguments = args;
            bool brokeDueToError = false;
            foreach (LispToken metadataToken in metadataBody.Items)
            {
                if (metadataToken.Type != LispDataType.List)
                {
                    runtimeContext?.RaiseRuntimeError(metadataToken, RuntimeErrorType.InvalidMetadata, "Metadata entry is not a list");
                    metadata = null;
                    return false;
                }
                LispList metadataEntry = (LispList)metadataToken;
                if (metadataEntry.Items.Count < 1 || metadataEntry.Items[0].Type != LispDataType.Symbol)
                {
                    runtimeContext?.RaiseRuntimeError(metadataEntry, RuntimeErrorType.InvalidMetadata, "Metadata entry does not contain an identifying symbol");
                    metadata = null;
                    return false;
                }
                LispSymbol identifyingSymbol = (LispSymbol)metadataEntry.Items[0];
                bool symbolHandled = false;
                foreach (MetadataParser parser in Parsers)
                {
                    if (parser.CanParse(identifyingSymbol))
                    {
                        if (!usedParsers.Contains(parser))
                        {
                            usedParsers.Add(parser);
                        }
                        MetadataEntry parsedEntry;
                        if (!parser.Parse(runtimeContext, metadataEntry, args, out parsedEntry))
                        {
                            break;
                        }
                        metadataResult.Entries.Add(parsedEntry);
                        symbolHandled = true;
                        break;
                    }
                }
                if (!symbolHandled)
                {
                    runtimeContext?.RaiseRuntimeError(identifyingSymbol, RuntimeErrorType.UnknownSymbolMeaning, "Unknown metadata type");
                    brokeDueToError = true;
                    break;
                }
            }
            if (runtimeContext != null)
            {
                runtimeContext.RuntimeError -= runtimeErrorHandler;
            }
            foreach (MetadataParser parser in usedParsers)
            {
                parser.Reset();
            }
            if (brokeDueToError)
            {
                metadata = null;
                return false;
            } else
            {
                metadata = metadataResult;
                return true;
            }
        }

        private LispSymbol[] definedArguments;

        public ArgumentMetadataEntry GetArgument(int i)
        {
            if (i < 0 || i >= definedArguments.Length)
            {
                throw new IndexOutOfRangeException();
            }
            MetadataEntry? queryResult = Entries.Where((MetadataEntry entry) => (entry.Type == MetadataEntryType.Argument && ((ArgumentMetadataEntry)entry).ParamIndex == i)).FirstOrDefault((MetadataEntry)null);
            if (queryResult == null)
            {
                return ArgumentMetadataEntry.CreateDefault(definedArguments[i], i);
            }
            else
            {
                return (ArgumentMetadataEntry)queryResult;
            }
        }
        public ArgumentMetadataEntry GetArgument(string argName)
        {
            for (int i = 0; i < definedArguments.Length; i++)
            {
                if (definedArguments[i].Value == argName)
                {
                    return GetArgument(i);
                }
            }
            throw new ArgumentException("argName is not the name of a parameter for the lisp function");
        }
    }
    public abstract class MetadataParser
    {
        public abstract bool CanParse(LispSymbol identifier);
        public abstract bool Parse(RuntimeContext? runtimeContext, LispList metadataEntryList, LispSymbol[] routineArgs, out MetadataEntry metadataEntry);
        public abstract void Reset();

        protected bool tryParseAttributeList(RuntimeContext? runtimeContext, LispList attrList, out MetadataAttributes attributes)
        {
            if (attrList.Items.Count % 2 > 0)
            {
                runtimeContext?.RaiseRuntimeError(attrList.Items[attrList.Items.Count - 1], RuntimeErrorType.InvalidMetadata, "Expected 1 more value in attribute list");
                attributes = null;
                return false;
            }
            List<MetadataAttribute> parsedAttributes = new List<MetadataAttribute>();
            LispSymbol? lastAttrSymbol = null;
            foreach (LispToken attrToken in attrList.Items)
            {
                if (lastAttrSymbol == null)
                {
                    if (attrToken.Type != LispDataType.Symbol)
                    {
                        runtimeContext?.RaiseRuntimeError(attrToken, RuntimeErrorType.InvalidMetadata, "Expected symbol next in attribute list");
                        attributes = null;
                        return false;
                    }
                    lastAttrSymbol = (LispSymbol)attrToken;
                } else
                {
                    if (attrToken.Type != LispDataType.List)
                    {
                        runtimeContext?.RaiseRuntimeError(attrToken, RuntimeErrorType.InvalidMetadata, "Expected list next in attribute list");
                        attributes = null;
                        return false;
                    }
                    parsedAttributes.Add(new MetadataAttribute(lastAttrSymbol, (LispList)attrToken));
                    lastAttrSymbol = null;
                }
            }
            attributes = new MetadataAttributes(parsedAttributes.ToArray());
            return true;
        }
        protected bool tryParseTypeRestrictions(RuntimeContext? runtimeContext, LispList typeList, bool canBeNone, out MetadataTypeRestrictions typeRestrictions)
        {
            if (typeList.Items.Count == 1 && typeList.Items[0].Type == LispDataType.Symbol && ((LispSymbol)typeList.Items[0]).Value == "none")
            {
                if (canBeNone)
                {
                    typeRestrictions = MetadataTypeRestrictions.NoType;
                    return true;
                } else
                {
                    runtimeContext?.RaiseRuntimeError(typeList.Items[0], RuntimeErrorType.InvalidMetadata, "'none' type restriction is not valid for the metadata type");
                    typeRestrictions = null;
                    return false;
                }
            }
            foreach (LispToken token in typeList.Items)
            {
                if (token.Type != LispDataType.String)
                {
                    runtimeContext?.RaiseRuntimeError(token, RuntimeErrorType.InvalidMetadata, "type restriction list must be all strings or symbol 'none'");
                    typeRestrictions = null;
                    return false;
                }
            }
            bool typeStrConversionsFailed = false;
            LispString failedConversion = null;
            LispTypeInfo?[] restrictions = typeList.Items.Cast<LispString>().Select((LispString typeStr) =>
            {
                if (typeStrConversionsFailed)
                {
                    return null;
                }
                LispTypeInfo type;
                if (!LispTypeInfo.TryParseFromTypeStr(typeStr.Value, out type))
                {
                    typeStrConversionsFailed = true;
                    failedConversion = typeStr;
                    return null;
                }
                return type;
            }).ToArray();
            if (typeStrConversionsFailed)
            {
                runtimeContext?.RaiseRuntimeError(failedConversion, RuntimeErrorType.InvalidMetadata, "type restriction list contains invalid typestr value");
                typeRestrictions = null;
                return false;
            }
            typeRestrictions = new MetadataTypeRestrictions(restrictions);
            return true;
        }
    }
    class GeneralAnnotationParser : MetadataParser
    {
        private bool generalAnnotationParsed { get; set; } = false;

        public override bool CanParse(LispSymbol identifier)
        {
            return identifier.Value == "annotation";
        }

        public override bool Parse(RuntimeContext? runtimeContext, LispList metadataEntryList, LispSymbol[] routineArgs, out MetadataEntry metadataEntry)
        {
            if (generalAnnotationParsed)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList, RuntimeErrorType.InvalidMetadata, "Only one general annotation is allowed");
                metadataEntry = null;
                return false;
            }
            List<LispToken> items = metadataEntryList.Items;
            if (items.Count != 2)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList, RuntimeErrorType.InvalidMetadata, "Annotation metadata should have exactly 1 string field");
                metadataEntry = null;
                return false;
            }
            if (items[1].Type != LispDataType.String)
            {
                runtimeContext?.RaiseRuntimeError(items[1], RuntimeErrorType.InvalidMetadata, "Expected string");
                metadataEntry = null;
                return false;
            }
            LispString annotationToken = (LispString)items[1];
            metadataEntry = new GeneralAnnotationMetadataEntry(annotationToken.Value);
            generalAnnotationParsed = true;
            return true;
        }

        public override void Reset()
        {
            generalAnnotationParsed = false;
        }
    }
    class ReturnParamParser : MetadataParser
    {
        private bool returnParamParsed { get; set; } = false;

        public override bool CanParse(LispSymbol identifier)
        {
            return identifier.Value == "returnparam";
        }

        public override bool Parse(RuntimeContext? runtimeContext, LispList metadataEntryList, LispSymbol[] routineArgs, out MetadataEntry metadataEntry)
        {
            if (returnParamParsed)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList, RuntimeErrorType.InvalidMetadata, "Only one return param is allowed");
                metadataEntry = null;
                return false;
            }
            if (metadataEntryList.Items.Count < 3 || metadataEntryList.Items.Count > 4)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList, RuntimeErrorType.InvalidMetadata, "Incorrect number of items for a returnparam metadata entry");
                metadataEntry = null;
                return false;
            }
            LispList typeRestrictionList;
            LispList attributeList;
            string annotation = "";
            if (metadataEntryList.Items[1].Type != LispDataType.List)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList.Items[1], RuntimeErrorType.InvalidMetadata, "Expected type restriction list");
                metadataEntry = null;
                return false;
            } else
            {
                typeRestrictionList = (LispList)metadataEntryList.Items[1];
            }
            if (metadataEntryList.Items[2].Type != LispDataType.List)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList.Items[2], RuntimeErrorType.InvalidMetadata, "Expected attribute list");
                metadataEntry = null;
                return false;
            } else
            {
                attributeList = (LispList)metadataEntryList.Items[2];
            }
            if (metadataEntryList.Items.Count > 3)
            {
                if (metadataEntryList.Items[3].Type != LispDataType.String)
                {
                    runtimeContext?.RaiseRuntimeError(metadataEntryList.Items[3], RuntimeErrorType.InvalidMetadata, "Expected annotation string");
                    metadataEntry = null;
                    return false;
                }
                else
                {
                    annotation = ((LispString)metadataEntryList.Items[3]).Value;
                }
            }
            MetadataAttributes parsedAttributes;
            MetadataTypeRestrictions parsedTypeRestrictions;
            if (!tryParseAttributeList(runtimeContext, attributeList, out parsedAttributes))
            {
                metadataEntry = null;
                return false;
            }
            if (!tryParseTypeRestrictions(runtimeContext, typeRestrictionList, true, out parsedTypeRestrictions))
            {
                metadataEntry = null;
                return false;
            }
            metadataEntry = new ReturnMetadataEntry(parsedAttributes, parsedTypeRestrictions)
            {
                Annotation = annotation
            };
            returnParamParsed = true;
            return true;
        }

        public override void Reset()
        {
            returnParamParsed = false;
        }
    }
    class ArgumentParser : MetadataParser
    {
        private HashSet<string> annotatedArgs = new HashSet<string>();

        public override bool CanParse(LispSymbol identifier)
        {
            return identifier.Value == "argument";
        }

        public override bool Parse(RuntimeContext? runtimeContext, LispList metadataEntryList, LispSymbol[] routineArgs, out MetadataEntry metadataEntry)
        {
            if (metadataEntryList.Items.Count < 4 || metadataEntryList.Items.Count > 5)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList, RuntimeErrorType.InvalidMetadata, "Incorrect number of items for an argument metadata entry");
                metadataEntry = null;
                return false;
            }
            LispSymbol argSymbol;
            LispList typeRestrictionList;
            LispList attributeList;
            string annotation = "";
            if (metadataEntryList.Items[1].Type != LispDataType.Symbol)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList.Items[1], RuntimeErrorType.InvalidMetadata, "Expected argument specifier");
                metadataEntry = null;
                return false;
            } else
            {
                argSymbol = (LispSymbol)metadataEntryList.Items[1];
            }
            if (metadataEntryList.Items[2].Type != LispDataType.List)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList.Items[2], RuntimeErrorType.InvalidMetadata, "Expected type restriction list");
                metadataEntry = null;
                return false;
            }
            else
            {
                typeRestrictionList = (LispList)metadataEntryList.Items[2];
            }
            if (metadataEntryList.Items[3].Type != LispDataType.List)
            {
                runtimeContext?.RaiseRuntimeError(metadataEntryList.Items[3], RuntimeErrorType.InvalidMetadata, "Expected attribute list");
                metadataEntry = null;
                return false;
            }
            else
            {
                attributeList = (LispList)metadataEntryList.Items[3];
            }
            if (metadataEntryList.Items.Count > 4)
            {
                if (metadataEntryList.Items[4].Type != LispDataType.String)
                {
                    runtimeContext?.RaiseRuntimeError(metadataEntryList.Items[4], RuntimeErrorType.InvalidMetadata, "Expected annotation string");
                    metadataEntry = null;
                    return false;
                }
                else
                {
                    annotation = ((LispString)metadataEntryList.Items[4]).Value;
                }
            }
            if (annotatedArgs.Contains(argSymbol.Value))
            {
                runtimeContext?.RaiseRuntimeError(argSymbol, RuntimeErrorType.InvalidMetadata, "Argument was already detailed in a previous metadata entry");
                metadataEntry = null;
                return false;
            }
            int argIndex = -1;
            for (int i = 0; i < routineArgs.Length; i++)
            {
                if (routineArgs[i].Value == argSymbol.Value)
                {
                    argIndex = i;
                    annotatedArgs.Add(argSymbol.Value);
                    break;
                }
            }
            if (argIndex < 0)
            {
                runtimeContext?.RaiseRuntimeError(argSymbol, RuntimeErrorType.InvalidMetadata, "Given symbol is not an argument in the function");
                metadataEntry = null;
                return false;
            }
            MetadataAttributes parsedAttributes;
            MetadataTypeRestrictions parsedTypeRestrictions;
            if (!tryParseAttributeList(runtimeContext, attributeList, out parsedAttributes))
            {
                metadataEntry = null;
                return false;
            }
            if (!tryParseTypeRestrictions(runtimeContext, typeRestrictionList, false, out parsedTypeRestrictions))
            {
                metadataEntry = null;
                return false;
            }
            metadataEntry = new ArgumentMetadataEntry(argSymbol, argIndex, parsedAttributes, parsedTypeRestrictions)
            {
                Annotation = annotation
            };
            return true;
        }

        public override void Reset()
        {
            annotatedArgs.Clear();
        }
    }
    public abstract class MetadataEntry
    {
        public abstract MetadataEntryType Type { get; }
        public virtual bool IsParamType { get => false; }
        public virtual Guid? ExtendedMetadataType { get => null; }
        public string Annotation { get; set; } = string.Empty;
    }
    public class GeneralAnnotationMetadataEntry : MetadataEntry
    {
        public override MetadataEntryType Type => MetadataEntryType.GeneralAnnotation;
        public GeneralAnnotationMetadataEntry(string text)
        {
            Annotation = text;
        }
        public static GeneralAnnotationMetadataEntry Default { get; } = new GeneralAnnotationMetadataEntry("");
    }
    public abstract class ParamMetadataEntry : MetadataEntry
    {
        public override bool IsParamType => true;
        public MetadataTypeRestrictions TypeRestrictions { get; set; }
        public MetadataAttributes Attributes { get; set; }
    }
    public class ArgumentMetadataEntry : ParamMetadataEntry
    {
        public override MetadataEntryType Type => MetadataEntryType.Argument;
        public LispSymbol Param { get; set; }
        public int ParamIndex { get; set; }
        public ArgumentMetadataEntry(LispSymbol param, int paramIndex, MetadataAttributes attributes, MetadataTypeRestrictions typeRestrictions)
        {
            Param = param;
            ParamIndex = paramIndex;
            Attributes = attributes;
            TypeRestrictions = typeRestrictions;
        }
        public static ArgumentMetadataEntry CreateDefault(LispSymbol param, int paramIndex)
        {
            return new ArgumentMetadataEntry(param, paramIndex, MetadataAttributes.NoAttributes, MetadataTypeRestrictions.AnyType);
        }
    }
    public class ReturnMetadataEntry : ParamMetadataEntry
    {
        public override MetadataEntryType Type => MetadataEntryType.Return;
        public ReturnMetadataEntry(MetadataAttributes attributes, MetadataTypeRestrictions typeRestrictions)
        {
            Attributes = attributes;
            TypeRestrictions = typeRestrictions;
        }
        public static ReturnMetadataEntry Default { get; } = new ReturnMetadataEntry(MetadataAttributes.NoAttributes, MetadataTypeRestrictions.AnyType);
    }
    public class AnyArgumentMetadataEntry : ParamMetadataEntry
    {
        public static AnyArgumentMetadataEntry Default { get; } = new AnyArgumentMetadataEntry(MetadataAttributes.NoAttributes, MetadataTypeRestrictions.AnyType);

        public override MetadataEntryType Type => MetadataEntryType.AnyArgument;
        public AnyArgumentMetadataEntry(MetadataAttributes attributes, MetadataTypeRestrictions typeRestrictions)
        {
            Attributes = attributes;
            TypeRestrictions = typeRestrictions;
        }
    }
    public class MetadataAttributes
    {
        public MetadataAttribute[] Attributes { get; set; }

        public static MetadataAttributes NoAttributes { get; } = new MetadataAttributes(new MetadataAttribute[0]);

        public MetadataAttributes(MetadataAttribute[] attributes)
        {
            Attributes = attributes;
        }

        public bool HasAttributes
        {
            get
            {
                return Attributes.Length > 0;
            }
        }

        public MetadataAttribute GetAttribute(string attrName)
        {
            return Attributes.Where((MetadataAttribute attribute) => attribute.Attribute.Value == attrName).First();
        }

        public bool TryGetAttribute(string attrName, out MetadataAttribute attribute)
        {
            MetadataAttribute queryResult = Attributes.Where((MetadataAttribute attribute) => attribute.Attribute.Value == attrName).FirstOrDefault(new MetadataAttribute(null, null));
            if (queryResult.Attribute == null)
            {
                attribute = default(MetadataAttribute);
                return false;
            } else
            {
                attribute = queryResult;
                return true;
            }
        }

        public bool HasAttribute(string attrName)
        {
            return Attributes.Where((MetadataAttribute attribute) => attribute.Attribute.Value == attrName).Any();
        }
    }
    public struct MetadataAttribute
    {
        public LispSymbol Attribute { get; set; }
        public LispList AttributeArgs { get; set; }
        public MetadataAttribute(LispSymbol attribute, LispList attributeArgs)
        {
            Attribute = attribute;
            AttributeArgs = attributeArgs;
        }
    }
    public class MetadataTypeRestrictions
    {
        public LispTypeInfo[]? TypeRestrictions { get; set; }

        public static MetadataTypeRestrictions AnyType { get; } = new MetadataTypeRestrictions(new LispTypeInfo[0]);
        public static MetadataTypeRestrictions NoType { get; } = new MetadataTypeRestrictions(null);

        public static MetadataTypeRestrictions Merge(MetadataTypeRestrictions a, MetadataTypeRestrictions b)
        {
            if (a.IsNoType)
            {
                return b;
            }
            if (b.IsNoType)
            {
                return a;
            }
            if (a.IsAnyType || b.IsAnyType)
            {
                return MetadataTypeRestrictions.AnyType;
            }
            return new MetadataTypeRestrictions(a.TypeRestrictions.Concat(b.TypeRestrictions).ToArray());
        }

        public MetadataTypeRestrictions(LispTypeInfo[]? typeRestrictions)
        {
            TypeRestrictions = typeRestrictions;
        }

        public bool IsAnyType
        {
            get
            {
                return (TypeRestrictions != null && TypeRestrictions.Length == 0);
            }
        }
        public bool IsNoType
        {
            get
            {
                return (TypeRestrictions == null);
            }
        }

        public void Include(LispTypeInfo type)
        {
            if (IsNoType)
            {
                TypeRestrictions = new LispTypeInfo[1] { type };
            } else if (!IsAnyType)
            {
                TypeRestrictions = TypeRestrictions.Append(type).ToArray();
            }
        }

        public bool CanBePassed(MetadataTypeRestrictions passedReturnType)
        {
            if (this.IsAnyType)
            {
                return true;
            }
            if (this.IsNoType)
            {
                return false;
            }
            if (passedReturnType.IsAnyType)
            {
                return true;
            }
            if (passedReturnType.IsNoType)
            {
                return false;
            }
            foreach (LispTypeInfo passedRestriction in passedReturnType.TypeRestrictions)
            {
                if (CanBePassed(passedRestriction))
                {
                    return true;
                }
            }
            return false;
        }
        public bool CanBePassed(LispTypeInfo passedReturnType)
        {
            if (this.IsAnyType)
            {
                return true;
            }
            if (this.IsNoType)
            {
                return false;
            } else
            {
                return canBePassedCore(passedReturnType);
            }
        }
        private bool canBePassedCore(LispTypeInfo passedReturnType)
        {
            foreach (LispTypeInfo restriction in TypeRestrictions)
            {
                if (restriction == passedReturnType)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool operator ==(MetadataTypeRestrictions a, MetadataTypeRestrictions b)
        {
            if (a.IsNoType && b.IsNoType)
            {
                return true;
            } else if (a.IsNoType || b.IsNoType)
            {
                return false;
            } else
            {
                return a.TypeRestrictions.SequenceEqual(b.TypeRestrictions, LispTypeInfoEqualityComparer.Comparer);
            }
        }

        public static bool operator !=(MetadataTypeRestrictions a, MetadataTypeRestrictions b)
        {
            return !(a == b);
        }
    }

    public enum MetadataEntryType
    {
        Argument,
        Return,
        AnyArgument,
        GeneralAnnotation,
        Extended
    }
}
