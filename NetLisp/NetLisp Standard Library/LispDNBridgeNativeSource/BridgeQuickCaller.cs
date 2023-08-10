using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LispDNBridgeNativeSource
{
    public delegate object? QuickCallInvoker(object? instance, object[] args);
    public delegate object? QuickCallFieldGetter(object? instance);
    public delegate void QuickCallFieldSetter(object? instance, object value);

    // this class compiles delegates at runtime for bridge symbols the first time that they are
    // called upon. this avoid reflection calls and significantly improves performance of all
    // language feature bridge functions.
    public class BridgeQuickCall
    {
        private class BuildExpressionLists
        {
            public List<ParameterExpression> locals = new List<ParameterExpression>();
            public List<Expression> preassignExpressions = new List<Expression>();
            public List<Expression> postassignExpressions = new List<Expression>();
        }
        public static QuickCallInvoker Create(MethodInfo method, ParameterInfo[] parameters)
        {
            ParameterExpression instParam = Expression.Parameter(typeof(object), "inst");
            ParameterExpression argsParam = Expression.Parameter(typeof(object[]), "args");
            BuildExpressionLists buildExpressionLists = new BuildExpressionLists();
            Expression callExpression;
            if (method.IsStatic)
            {
                callExpression = Expression.Call(method, generateParamExpressions(argsParam, parameters, buildExpressionLists));
            } else
            {
                callExpression = Expression.Call(Expression.Convert(instParam, method.DeclaringType), method, generateParamExpressions(argsParam, parameters, buildExpressionLists));
            }
            List<Expression> allExpressions = new List<Expression>();
            allExpressions.AddRange(buildExpressionLists.preassignExpressions);
            Type returnType = method.ReturnType;
            if (returnType == typeof(void))
            {
                allExpressions.Add(callExpression);
                buildExpressionLists.postassignExpressions.Add(Expression.Constant(null));
            } else
            {
                ParameterExpression methodReturnStore = Expression.Variable(typeof(object));
                buildExpressionLists.locals.Add(methodReturnStore);
                allExpressions.Add(Expression.Assign(methodReturnStore, Expression.Convert(callExpression, typeof(object))));
                buildExpressionLists.postassignExpressions.Add(methodReturnStore);
            }
            allExpressions.AddRange(buildExpressionLists.postassignExpressions);
            LambdaExpression expr = Expression.Lambda<QuickCallInvoker>(
                Expression.Block(buildExpressionLists.locals, allExpressions),
                instParam,
                argsParam
                );
            return (QuickCallInvoker)expr.Compile();
        }

        public static QuickCallInvoker Create(ConstructorInfo constructor, ParameterInfo[] parameters)
        {
            ParameterExpression instParam = Expression.Parameter(typeof(object), "inst"); // not used
            ParameterExpression argsParam = Expression.Parameter(typeof(object[]), "args");
            BuildExpressionLists buildExpressionLists = new BuildExpressionLists();
            Expression newExpression;
            newExpression = Expression.New(constructor, generateParamExpressions(argsParam, parameters, buildExpressionLists));
            List<Expression> allExpressions = new List<Expression>();
            allExpressions.AddRange(buildExpressionLists.preassignExpressions);
            ParameterExpression constructorReturnStore = Expression.Variable(typeof(object));
            buildExpressionLists.locals.Add(constructorReturnStore);
            allExpressions.Add(Expression.Assign(constructorReturnStore, Expression.Convert(newExpression, typeof(object))));
            buildExpressionLists.postassignExpressions.Add(constructorReturnStore);
            allExpressions.AddRange(buildExpressionLists.postassignExpressions);
            LambdaExpression expr = Expression.Lambda<QuickCallInvoker>(
                Expression.Block(buildExpressionLists.locals, allExpressions),
                instParam,
                argsParam
                );
            return (QuickCallInvoker)expr.Compile();
        }

        public static QuickCallFieldGetter CreateFieldGetter(FieldInfo field)
        {
            ParameterExpression inst = Expression.Parameter(typeof(object), "inst");
            Expression instExpression = null;
            if (!field.IsStatic)
            {
                instExpression = Expression.Convert(inst, field.DeclaringType);
            }
            LambdaExpression expr = Expression.Lambda<QuickCallFieldGetter>(
                Expression.Convert(Expression.Field(instExpression, field), typeof(object)),
                inst
                );
            return (QuickCallFieldGetter)expr.Compile();
        }

        public static QuickCallFieldSetter CreateFieldSetter(FieldInfo field)
        {
            ParameterExpression inst = Expression.Parameter(typeof(object), "inst");
            ParameterExpression newValue = Expression.Parameter(typeof(object), "newValue");
            Expression instExpression = null;
            if (!field.IsStatic)
            {
                instExpression = Expression.Convert(inst, field.DeclaringType);
            }
            LambdaExpression expr = Expression.Lambda<QuickCallFieldSetter>(
                Expression.Assign(Expression.Field(instExpression, field), Expression.Convert(newValue, field.FieldType)),
                inst,
                newValue
                );
            return (QuickCallFieldSetter)expr.Compile();
        }

        private static IEnumerable<Expression> generateParamExpressions(ParameterExpression argsParam, ParameterInfo[] parameters, BuildExpressionLists exprLists)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo paramInfo = parameters[i];
                Type paramType = paramInfo.ParameterType;
                if (paramType.IsByRef)
                {
                    Type elType = paramType.GetElementType();
                    ParameterExpression refStorageLocal = Expression.Variable(elType);
                    exprLists.locals.Add(refStorageLocal);
                    if (!paramInfo.IsOut)
                    {
                        exprLists.preassignExpressions.Add(Expression.Assign(refStorageLocal, Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(i)), elType)));
                    }
                    exprLists.postassignExpressions.Add(Expression.Assign(Expression.ArrayAccess(argsParam, Expression.Constant(i)), Expression.Convert(refStorageLocal, typeof(object))));
                    yield return refStorageLocal;
                } else
                {
                    yield return Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(i)), paramInfo.ParameterType);
                }
            }
        }
    }
}
