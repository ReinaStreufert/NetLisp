using NetLisp.Runtime;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NetLisp.Text
{
    static class ModuleConfigParser
    {
        public static bool TryParseModuleConfig(string moduleConfigText, out ModuleConfig moduleConfig)
        {
            JObject moduleConfigJson;
            try
            {
                moduleConfigJson = JObject.Parse(moduleConfigText);
            } catch
            {
                moduleConfig = null;
                return false;
            }
            ModuleConfig result = new ModuleConfig();
            JValue symbolName = validateField<JValue>(moduleConfigJson, "symbol-name", JTokenType.String);
            JValue fullName = validateField<JValue>(moduleConfigJson, "full-name", JTokenType.String);
            JArray sourceChain = validateField<JArray>(moduleConfigJson, "source-chain", JTokenType.Array);
            if (anyNull(symbolName, fullName, sourceChain))
            {
                moduleConfig = null;
                return false;
            }
            result.SymbolName = symbolName.Value<string>();
            result.FullName = fullName.Value<string>();
            foreach (JToken token in sourceChain)
            {
                if (token.Type != JTokenType.Object)
                {
                    moduleConfig = null;
                    return false;
                }
                SourceInfo sourceInfo = parseSourceInfo((JObject)token);
                if (sourceInfo != null)
                {
                    result.SourceChain.Add(sourceInfo);
                } else
                {
                    moduleConfig = null;
                    return false;
                }
            }
            moduleConfig = result;
            return true;
        }
        private static SourceInfo parseSourceInfo(JObject sourceInfoJson)
        {
            SourceInfo result = new SourceInfo();
            JValue isNativeSource = validateField<JValue>(sourceInfoJson, "is-native-source", JTokenType.Boolean);
            JValue path = validateField<JValue>(sourceInfoJson, "path", JTokenType.String);
            JValue innerType = validateField<JValue>(sourceInfoJson, "inner-type", JTokenType.String);
            if (anyNull(isNativeSource, path))
            {
                return null;
            }
            result.IsNativeSource = isNativeSource.Value<bool>();
            result.Path = path.Value<string>();
            if (innerType == null)
            {
                result.InnerType = null;
            } else
            {
                result.InnerType = innerType.Value<string>();
            }
            return result;
        }
        private static T? validateField<T>(JObject obj, string fieldName, JTokenType expectedType) where T : JToken
        {
            if (obj.ContainsKey(fieldName) && obj[fieldName].Type == expectedType)
            {
                return (T)obj[fieldName];
            } else
            {
                return null;
            }
        }
        private static bool anyNull(params object[] args)
        {
            foreach (object o in args)
            {
                if (o == null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
