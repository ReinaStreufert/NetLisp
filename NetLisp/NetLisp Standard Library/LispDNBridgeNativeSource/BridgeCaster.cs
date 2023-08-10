using NetLisp.Data;
using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LispDNBridgeNativeSource
{
    // provides methods to cast between lisp types and dot net types
    public static class BridgeCaster
    {
        private static List<BridgeCastOperation> bridgeCasters = new List<BridgeCastOperation>();

        static BridgeCaster()
        {
            bridgeCasters.Add(new DotNetInstanceCaster());
            bridgeCasters.Add(new DotNetTypeCaster());
            bridgeCasters.Add(new LispListCaster());
            bridgeCasters.Add(new LispNumberCaster());
            bridgeCasters.Add(new LispBooleanCaster());
            bridgeCasters.Add(new LispStringCaster());
        }

        public static bool TryCast(LispToken lispObject, Type toType, out object result)
        {
            bool castingEnum = false;
            Type enumType = null;
            if (toType.IsEnum)
            {
                castingEnum = true;
                enumType = toType;
                toType = toType.GetEnumUnderlyingType();
            }
            object castResult;
            foreach (BridgeCastOperation op in bridgeCasters.Reverse<BridgeCastOperation>())
            {
                if (op.TryCast(lispObject, toType, out castResult))
                {
                    if (castingEnum)
                    {
                        result = Enum.ToObject(enumType, castResult);
                    } else
                    {
                        result = castResult;
                    }
                    return true;
                }
            }
            result = null;
            return false;
        }
        public static bool TryCast(object obj, out LispToken lispObject)
        {
            if (obj == null)
            {
                lispObject = new LispList();
                return true;
            }
            foreach (BridgeCastOperation op in bridgeCasters.Reverse<BridgeCastOperation>())
            {
                if (op.TryCast(obj, out lispObject))
                {
                    return true;
                }
            }
            lispObject = null;
            return false;
        }
        public static void RegisterExtendedBridgeCaster(BridgeCastOperation operation)
        {
            bridgeCasters.Add(operation);
        }
    }

    public abstract class BridgeCastOperation
    {
        public abstract bool TryCast(LispToken fromToken, Type toType, out object result);
        public abstract bool TryCast(object fromObject, out LispToken result);
    }

    class LispNumberCaster : BridgeCastOperation
    {
        public override bool TryCast(LispToken fromToken, Type toType, out object result)
        {
            if (fromToken.Type != LispDataType.Number)
            {
                result = null;
                return false;
            }
            double num = ((LispNumber)fromToken).Value;
            
            if (toType == typeof(double))
            {
                result = num;
                return true;
            } else if (toType == typeof(float))
            {
                result = (float)num;
                return true;
            } else if (toType == typeof(decimal))
            {
                result = (decimal)num;
                return true;
            }
            try // integer casts may overflow
            {
                if (toType == typeof(sbyte))
                {
                    result = (sbyte)num;
                    return true;
                } else if (toType == typeof(byte))
                {
                    result = (byte)num;
                    return true;
                } else if (toType == typeof(ushort))
                {
                    result = (ushort)num;
                    return true;
                } else if (toType == typeof(short))
                {
                    result = (short)num;
                    return true;
                } else if (toType == typeof(uint))
                {
                    result = (uint)num;
                    return true;
                } else if (toType == typeof(int))
                {
                    result = (int)num;
                    return true;
                } else if (toType == typeof(ulong))
                {
                    result = (ulong)num;
                    return true;
                } else if (toType == typeof(long))
                {
                    result = (long)num;
                    return true;
                }
            } catch (OverflowException)
            {
                result = null;
                return false;
            }
            result = null;
            return false;
        }
        public override bool TryCast(object fromObject, out LispToken result)
        {
            Type fromType = fromObject.GetType();
            if (fromType.IsEnum || fromType == typeof(byte) || fromType == typeof(sbyte) ||
                fromType == typeof(ushort) || fromType == typeof(short) || fromType == typeof(uint) ||
                fromType == typeof(int) || fromType == typeof(ulong) || fromType == typeof(long) ||
                fromType == typeof(decimal) || fromType == typeof(float) || fromType == typeof(double))
            {
                result = new LispNumber(Convert.ToDouble(fromObject));
                return true;
            } else
            {
                result = null;
                return false;
            }
        }
    }

    class LispStringCaster : BridgeCastOperation
    {
        public override bool TryCast(LispToken fromToken, Type toType, out object result)
        {
            if (fromToken.Type == LispDataType.String && toType == typeof(string))
            {
                result = ((LispString)fromToken).Value;
                return true;
            } else
            {
                result = null;
                return false;
            }
        }

        public override bool TryCast(object fromObject, out LispToken result)
        {
            if (fromObject.GetType() == typeof(string))
            {
                result = new LispString((string)fromObject);
                return true;
            } else
            {
                result = null;
                return false;
            }
        }
    }

    class LispBooleanCaster : BridgeCastOperation
    {
        public override bool TryCast(LispToken fromToken, Type toType, out object result)
        {
            if (fromToken.Type == LispDataType.Boolean && toType == typeof(bool))
            {
                result = ((LispBoolean)fromToken).Value;
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        public override bool TryCast(object fromObject, out LispToken result)
        {
            if (fromObject.GetType() == typeof(bool))
            {
                result = new LispBoolean((bool)fromObject);
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }
    }

    class LispListCaster : BridgeCastOperation
    {
        public override bool TryCast(LispToken fromToken, Type toType, out object result)
        {
            if (fromToken.Type == LispDataType.List)
            {
                LispList itemList = (LispList)fromToken;

                if (toType.IsArray)
                {
                    Type elType = toType.GetElementType();
                    Array resultArray = Array.CreateInstance(elType, itemList.Items.Count);

                    for (int i = 0; i < itemList.Items.Count; i++)
                    {
                        LispToken item = itemList.Items[i];
                        object itemCastResult;
                        if (!BridgeCaster.TryCast(item, elType, out itemCastResult))
                        {
                            result = null;
                            return false;
                        }
                        resultArray.SetValue(itemCastResult, i);
                    }
                    result = resultArray;
                    return true;
                } else if (itemList.Items.Count == 0 && (!toType.IsValueType || (Nullable.GetUnderlyingType(toType) != null)))
                {
                    result = null;
                    return true; // empty list casts to null if the case of a non array type. 
                } else
                {
                    result = null;
                    return false;
                }
            } else
            {
                result = null;
                return false;
            }
        }

        public override bool TryCast(object fromObject, out LispToken result)
        {
            if (fromObject.GetType().IsArray)
            {
                Array fromArray = (Array)fromObject;
                LispToken[] toArray = new LispToken[fromArray.Length];

                for (int i = 0; i < fromArray.Length; i++)
                {
                    object? item = fromArray.GetValue(i);
                    if (item == null)
                    {
                        toArray[i] = new LispList();
                    }
                    LispToken itemCastResult;
                    if (BridgeCaster.TryCast(item, out itemCastResult))
                    {
                        toArray[i] = itemCastResult;
                    } else
                    {
                        result = null;
                        return false;
                    }
                }
                result = new LispList(toArray.ToList());
                return true;
            } else
            {
                result = null;
                return false;
            }
        }
    }
    class DotNetInstanceCaster : BridgeCastOperation
    {
        public override bool TryCast(LispToken fromToken, Type toType, out object result)
        {
            if (fromToken.Type == LispDataType.ExtendedType && ((ExtendedLispToken)fromToken).ExtendedTypeInfo.ExtendedTypeGuid == DotnetInstance.DotnetInstanceExtendedTypeInfo.ExtendedTypeGuid)
            {
                DotnetInstance inst = (DotnetInstance)fromToken;
                if (inst.Instance.GetType().IsAssignableTo(toType))
                {
                    result = inst.Instance;
                    return true;
                } else
                {
                    result = null;
                    return false;
                }
            } else
            {
                result = null;
                return false;
            }
        }

        public override bool TryCast(object fromObject, out LispToken result)
        {
            result = new DotnetInstance(fromObject);
            return true;
        }
    }
    class DotNetTypeCaster : BridgeCastOperation
    {
        public override bool TryCast(LispToken fromToken, Type toType, out object result)
        {
            if (toType is Type && fromToken.Type == LispDataType.ExtendedType && ((ExtendedLispToken)fromToken).ExtendedTypeInfo.ExtendedTypeGuid == DotnetType.DotnetTypeExtendedTypeInfo.ExtendedTypeGuid)
            {
                result = ((DotnetType)fromToken).TypeInstance;
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        public override bool TryCast(object fromObject, out LispToken result)
        {
            if (fromObject is Type)
            {
                result = new DotnetType((Type)fromObject); // jesus
                return true;
            } else
            {
                result = null;
                return false;
            }
        }
    }
}
