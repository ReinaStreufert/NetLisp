using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LispDNBridgeNativeSource
{
    abstract class ReturningDotNetBridge : LispSpecialForm
    {
        public bool ExactReturn { get; protected set; } = false;
        public abstract ReturningDotNetBridge CreateExactReturn();
    }
    class FieldBridge : ReturningDotNetBridge
    {
        public FieldInfo DotNetField { get; set; }
        public FieldBridge(FieldInfo dotNetField)
        {
            isStatic = dotNetField.IsStatic;
            if (dotNetField.IsLiteral || dotNetField.IsInitOnly)
            {
                canWrite = false;
            } else
            {
                canWrite = true;
            }
            DotNetField = dotNetField;
        }
        public override ReturningDotNetBridge CreateExactReturn()
        {
            return new FieldBridge(DotNetField) { ExactReturn = true, isStatic = isStatic };
        }

        private bool isStatic;
        private bool canWrite;
        private QuickCallFieldGetter nonReflectionGetter = null;
        private QuickCallFieldSetter nonReflectionSetter = null;

        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            int minArgs = 1;
            int maxArgs = 2;
            if (isStatic)
            {
                minArgs--;
                maxArgs--;
            }
            if (passedArgs.Count < minArgs || passedArgs.Count > maxArgs)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "dotnet field bridge expects 1 instance argument if non-static and 1 value argument if setting");
            } else if (!canWrite && passedArgs.Count == maxArgs)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.Other, "cannot set dotnet field bridge because field is not writeable (const or readonly)");
            }
            object instance = null;
            if (!isStatic)
            {
                DotnetInstance bridgeInstance = runtimeContext.Assert<DotnetInstance>(passedArgs[0], DotnetInstance.DotnetInstanceExtendedTypeInfo);
                Type bridgeInstType = bridgeInstance.Instance.GetType();
                Type declaringType = DotNetField.DeclaringType;
                if (bridgeInstType != declaringType && !bridgeInstType.IsAssignableTo(declaringType))
                {
                    runtimeContext.RaiseRuntimeError(bridgeInstance, RuntimeErrorType.ArgumentMismatchError, "provided instance did not match the declaring type for the field");
                }
                instance = bridgeInstance.Instance;
                passedArgs.RemoveAt(0);
            }
            if (passedArgs.Count == 1)
            {
                LispToken value = passedArgs[0];
                object valueDotNet;
                if (!BridgeCaster.TryCast(value, DotNetField.FieldType, out valueDotNet))
                {
                    runtimeContext.RaiseRuntimeError(value, RuntimeErrorType.Other, "Cannot convert from given lisp value to dotnet type " + DotNetField.FieldType.Name);
                } else
                {
                    if (nonReflectionSetter == null)
                    {
                        nonReflectionSetter = BridgeQuickCall.CreateFieldSetter(DotNetField);
                    }
                    nonReflectionSetter(instance, valueDotNet);
                }
            } else
            {
                if (nonReflectionGetter == null)
                {
                    nonReflectionGetter = BridgeQuickCall.CreateFieldGetter(DotNetField);
                }
                object? fieldValue = nonReflectionGetter(instance);
                if (ExactReturn)
                {
                    yield return new DotnetInstance(fieldValue);
                } else
                {
                    LispToken fieldValueLisp;
                    if (!BridgeCaster.TryCast(fieldValue, out fieldValueLisp))
                    {
                        // technically the bridge caster just does this but this will make the compiler happy
                        fieldValueLisp = new DotnetInstance(fieldValue);
                    }
                    yield return fieldValueLisp;
                }
            }
        }
    }
    class PropertyBridge : ReturningDotNetBridge
    {
        public PropertyInfo DotNetProperty { get; set; }

        private OverloadInfo getter = null;
        private OverloadInfo setter = null;
        private bool isStatic;

        public PropertyBridge(PropertyInfo dotNetProperty)
        {
            DotNetProperty = dotNetProperty;
            isStatic = dotNetProperty.IsStatic();
            MethodInfo getMeth = dotNetProperty.GetGetMethod(false);
            MethodInfo setMeth = dotNetProperty.GetSetMethod(false);
            if (getMeth != null)
            {
                getter = new OverloadInfo(getMeth);
            }
            if (setMeth != null)
            {
                setter = new OverloadInfo(setMeth);
            }
        }
        public override ReturningDotNetBridge CreateExactReturn()
        {
            return new PropertyBridge(DotNetProperty) { ExactReturn = true, isStatic = isStatic };
        }

        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            object instance = null;
            if (!isStatic)
            {
                DotnetInstance bridgeInstance = runtimeContext.Assert<DotnetInstance>(passedArgs[0], DotnetInstance.DotnetInstanceExtendedTypeInfo);
                Type bridgeInstType = bridgeInstance.Instance.GetType();
                Type declaringType = DotNetProperty.DeclaringType;
                if (bridgeInstType != declaringType && !bridgeInstType.IsAssignableTo(declaringType))
                {
                    runtimeContext.RaiseRuntimeError(bridgeInstance, RuntimeErrorType.ArgumentMismatchError, "provided instance did not match the declaring type for the field");
                }
                instance = bridgeInstance.Instance;
                passedArgs.RemoveAt(0);
            }
            object fieldValue;
            if (getter != null && getter.TryCallWithLispParams(runtimeContext, instance, passedArgs, ExactReturn, out fieldValue))
            {
                if (ExactReturn)
                {
                    yield return new DotnetInstance(fieldValue);
                }
                else
                {
                    LispToken fieldValueLisp;
                    if (!BridgeCaster.TryCast(fieldValue, out fieldValueLisp))
                    {
                        // technically the bridge caster just does this but this will make the compiler happy
                        fieldValueLisp = new DotnetInstance(fieldValue);
                    }
                    yield return fieldValueLisp;
                }
            }
            else if (setter != null && setter.TryCallWithLispParams(runtimeContext, instance, passedArgs, ExactReturn, out fieldValue))
            {
                yield break;
            } else
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.Other, "The arguments given did not match the public getter or setter for the property");
            }
        }
    }
    class OverloadInfo
    {
        public bool IsStatic { get; set; }
        public bool IsVoid { get; set; }
        public MethodInfo Method { get; set; }
        public ConstructorInfo Constructor { get; set; }
        public ParameterInfo[] Parameters { get; set; }

        private object[] callBuffer;
        private QuickCallInvoker nonReflectionInvoker = null;

        public OverloadInfo(MethodInfo method)
        {
            Method = method;
            Constructor = null;
            IsStatic = method.IsStatic;
            IsVoid = method.ReturnType == typeof(void);
            Parameters = method.GetParameters();
            callBuffer = new object[Parameters.Length];
        }
        public OverloadInfo(ConstructorInfo method)
        {
            Constructor = method;
            Method = null;
            IsStatic = true;
            IsVoid = false;
            Parameters = method.GetParameters();
            callBuffer = new object[Parameters.Length];
        }
        public bool TryCallWithLispParams(RuntimeContext runtimeContext, object? instance, List<LispToken> args, bool exactReturnSet, out object? returnVal)
        {
            if (args.Count > Parameters.Length || (!IsStatic && instance == null))
            {
                returnVal = null;
                return false;
            }
            // the lock allows async functions to safely call the same dotnet functions. theoretically i
            // could not use the shared callBuffer, but this could cause the "first-time" call to happen twice
            // at once on two threads and make both threads attempt to create and write nonReflectionInvoker
            lock (callBuffer)
            {
                bool containsRefArgs = false;
                for (int i = 0; i < callBuffer.Length; i++)
                {
                    ParameterInfo param = Parameters[i];
                    if (i >= args.Count)
                    {
                        if (param.IsOptional)
                        {
                            callBuffer[i] = param.DefaultValue;
                            continue;
                        }
                        else
                        {
                            returnVal = null;
                            return false;
                        }
                    }
                    if (param.ParameterType.IsByRef)
                    {
                        containsRefArgs = true;
                        if (args[i].Type != LispDataType.Symbol)
                        {
                            returnVal = null;
                            return false;
                        }
                        LispSymbol refSym = (LispSymbol)args[i];
                        if (param.IsOut)
                        {
                            callBuffer[i] = null;
                        }
                        else
                        {
                            object castedRef;
                            if (!BridgeCaster.TryCast(refSym.Evaluate(runtimeContext).First(), param.ParameterType.GetElementType(), out castedRef))
                            {
                                returnVal = null;
                                return false;
                            }
                            callBuffer[i] = castedRef;
                        }
                        continue;
                    }
                    object castedArg;
                    if (!BridgeCaster.TryCast(args[i], param.ParameterType, out castedArg))
                    {
                        returnVal = null;
                        return false;
                    }
                    callBuffer[i] = castedArg;
                }
                if (nonReflectionInvoker == null)
                {
                    if (Method == null)
                    {
                        nonReflectionInvoker = BridgeQuickCall.Create(Constructor, Parameters);
                    }
                    else
                    {
                        nonReflectionInvoker = BridgeQuickCall.Create(Method, Parameters);
                    }
                }
                returnVal = nonReflectionInvoker(instance, callBuffer);
                if (!containsRefArgs)
                {
                    return true;
                }
                // check for out/ref pass backs
                for (int i = 0; i < callBuffer.Length; i++)
                {
                    if (Parameters[i].ParameterType.IsByRef)
                    {
                        LispSymbol dest = (LispSymbol)args[i];
                        if (exactReturnSet)
                        {
                            runtimeContext.Scopes.CurrentScope.Set(dest.Value, new DotnetInstance(callBuffer[i]));
                        }
                        else
                        {
                            LispToken castedRefReturn;
                            BridgeCaster.TryCast(callBuffer[i], out castedRefReturn);
                            runtimeContext.Scopes.CurrentScope.Set(dest.Value, castedRefReturn);
                        }
                    }
                }
                return true;
            }
        }
    }
    class MethodGroupBridge : ReturningDotNetBridge
    {
        public List<OverloadInfo> Overloads { get; set; }
        public bool IsStatic { get; set; }
        public MethodGroupBridge(List<MethodInfo> overloads)
        {
            Overloads = new List<OverloadInfo>();
            for (int i = 0; i < overloads.Count; i++)
            {
                Overloads.Add(new OverloadInfo(overloads[i]));
            }
            IsStatic = overloads[0].IsStatic;
        }
        public MethodGroupBridge(MethodGroupBridge underlyingBridge, bool exactReturn)
        {
            Overloads = underlyingBridge.Overloads;
            IsStatic = underlyingBridge.IsStatic;
            ExactReturn = exactReturn;
        }
        public override ReturningDotNetBridge CreateExactReturn()
        {
            return new MethodGroupBridge(this, true);
        }
        public void AddOverload(MethodInfo methodInfo)
        {
            Overloads.Add(new OverloadInfo(methodInfo));
        }

        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            object instance = null;
            if (!IsStatic)
            {
                if (passedArgs.Count < 1)
                {
                    runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "non-static dotnet method bridge requires at least 1 argument for instance");
                }
                instance = runtimeContext.Assert<DotnetInstance>(passedArgs[0], DotnetInstance.DotnetInstanceExtendedTypeInfo).Instance;
                Type bridgeInstType = instance.GetType();
                Type declaringType = Overloads[0].Method.DeclaringType;
                if (bridgeInstType != declaringType && !bridgeInstType.IsAssignableTo(declaringType))
                {
                    runtimeContext.RaiseRuntimeError(passedArgs[0], RuntimeErrorType.ArgumentMismatchError, "provided instance did not match the declaring type for the field");
                }
                passedArgs.RemoveAt(0);
            }
            bool methodExecuted = false;
            object returnVal = null;
            bool isVoid = false;
            foreach (OverloadInfo overload in Overloads)
            {
                if (overload.Method.ContainsGenericParameters) { continue; }
                if (overload.TryCallWithLispParams(runtimeContext, instance, passedArgs, ExactReturn, out returnVal))
                {
                    methodExecuted = true;
                    isVoid = overload.IsVoid;
                    break;
                }
            }
            if (!methodExecuted)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "No suitable overload for the dotnet method was found");
            }
            if (!isVoid)
            {
                if (ExactReturn)
                {
                    yield return new DotnetInstance(returnVal);
                }
                else
                {
                    LispToken returnValCast;
                    if (!BridgeCaster.TryCast(returnVal, out returnValCast))
                    {
                        // this is impossible, ugh i wrote this stupidly and without thinking
                        yield break;
                    }
                    yield return returnValCast;
                }
            }
        }
    }
    class ConstructorBridge : LispSpecialForm
    {
        private OverloadInfo[] Overloads { get; set; }
        public ConstructorBridge(ConstructorInfo[] overloads)
        {
            Overloads = new OverloadInfo[overloads.Length];
            for (int i = 0; i < overloads.Length; i++)
            {
                Overloads[i] = new OverloadInfo(overloads[i]);
            }
        }

        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            object returnValue = null;
            bool constructorExecuted = false;
            foreach (OverloadInfo overload in Overloads)
            {
                if (overload.TryCallWithLispParams(runtimeContext, null, passedArgs, true, out returnValue))
                {
                    constructorExecuted = true;
                    break;
                }
            }
            if (!constructorExecuted)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "No constructor for the type matches the arguments given");
            }
            yield return new DotnetInstance(returnValue);
        }
    }
    class GenericT : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count < 2)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "generict requires at least 2 arguments");
            }
            Type genericType = runtimeContext.Assert<DotnetType>(passedArgs[0], DotnetType.DotnetTypeExtendedTypeInfo).TypeInstance;
            if (!genericType.IsGenericType)
            {
                runtimeContext.RaiseRuntimeError(passedArgs[0], RuntimeErrorType.ArgumentMismatchError, "cannot fill generic arguments because type is not generic");
            }
            int genericParamCount = genericType.GetGenericArguments().Length;
            if (passedArgs.Count - 1 != genericParamCount)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "type requires " + genericParamCount + " generic arguments");
            }
            Type[] typeParams = new Type[genericParamCount];
            for (int i = 1; i < passedArgs.Count; i++)
            {
                typeParams[i - 1] = runtimeContext.Assert<DotnetType>(passedArgs[i], DotnetType.DotnetTypeExtendedTypeInfo).TypeInstance;
            }
            Type constructedType = null;
            try
            {
                constructedType = genericType.MakeGenericType(typeParams);
            } catch (ArgumentException ex)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "type params given did not match type constraints on generic type");
            } catch (NotSupportedException ex)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "generic type requires construction on one of its derived classes instead of the base class");
            }
            yield return new DotnetType(constructedType);
        }
    }
    class GenericM : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count < 2)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "genericm requires at least 2 arguments");
            }
            MethodGroupBridge methodGroup = null;
            if (passedArgs[0].GetType() == typeof(MethodGroupBridge))
            {
                methodGroup = (MethodGroupBridge)passedArgs[0];
            }
            else
            {
                runtimeContext.RaiseRuntimeError(passedArgs[0], RuntimeErrorType.ArgumentMismatchError, "genericm takes a method bridge as the first argument");
            }
            Type[] typeParams = new Type[passedArgs.Count - 1];
            for (int i = 1; i < passedArgs.Count; i++)
            {
                typeParams[i - 1] = runtimeContext.Assert<DotnetType>(passedArgs[i], DotnetType.DotnetTypeExtendedTypeInfo).TypeInstance;
            }
            List<MethodInfo> constructedMethods = new List<MethodInfo>();
            foreach (OverloadInfo overload in methodGroup.Overloads)
            {
                if (overload.Method.ContainsGenericParameters && overload.Method.GetGenericArguments().Length == typeParams.Length)
                {
                    MethodInfo constructedMethod = null;
                    try
                    {
                        constructedMethod = overload.Method.MakeGenericMethod(typeParams);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                    catch (NotSupportedException)
                    {
                        runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.Other, "generic type requires construction on one of its derived classes instead of the base class");
                    }
                    constructedMethods.Add(constructedMethod);
                }
            }
            if (constructedMethods.Count < 1)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "No overloads could be constructed with the given type parameters");
            }
            yield return new MethodGroupBridge(constructedMethods);
        }
    }
    class ExactReturn : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 1)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "dnexactreturn takes 1 argument");
            }
            if (!passedArgs[0].GetType().IsAssignableTo(typeof(ReturningDotNetBridge)))
            {
                runtimeContext.RaiseRuntimeError(passedArgs[0], RuntimeErrorType.ArgumentMismatchError, "dnexactreturn takes a field, property, or method bridge. constructors already return exactly");
            }
            ReturningDotNetBridge bridge = (ReturningDotNetBridge)passedArgs[0];
            if (bridge.ExactReturn)
            {
                runtimeContext.RaiseRuntimeError(passedArgs[0], RuntimeErrorType.Other, "given method bridge already has exact return flag set");
            }
            yield return bridge.CreateExactReturn();
        }
    }
    class BridgeCast : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 2)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "dnbridgecast takes 2 arguments");
            }
            LispToken lispObject = passedArgs[0];
            Type toType = runtimeContext.Assert<DotnetType>(passedArgs[1], DotnetType.DotnetTypeExtendedTypeInfo).TypeInstance;
            object dnObject = null;
            if (!BridgeCaster.TryCast(lispObject, toType, out dnObject))
            {
                runtimeContext.RaiseRuntimeError(passedArgs[1], RuntimeErrorType.Other, "Cannot convert from the provided lisp type to dotnet type " + toType.Name);
            }
            yield return new DotnetInstance(dnObject);
        }
    }
    class EnumInst : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 2)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "dnenuminst takes 2 arguments");
            }
            DotnetType enumType = runtimeContext.Assert<DotnetType>(passedArgs[0], DotnetType.DotnetTypeExtendedTypeInfo);
            LispNumber enumValue = runtimeContext.Assert<LispNumber>(passedArgs[1], LispDataType.Number);
            if (!enumType.TypeInstance.IsEnum)
            {
                runtimeContext.RaiseRuntimeError(enumType, RuntimeErrorType.ArgumentMismatchError, "type given was not an enum");
            }
            Type underlyingType = enumType.TypeInstance.GetEnumUnderlyingType();
            yield return new DotnetInstance(Enum.ToObject(enumType.TypeInstance, Convert.ChangeType(enumValue.Value, underlyingType)));
        }
    }
    class LoadAsm : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 1)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "dnloadasm takes 1 arguments");
            }
            BridgeReflectionResources.LoadAsm(runtimeContext.Assert<LispString>(passedArgs[0], LispDataType.String).Value);
            yield break;
            
        }
    }
}
