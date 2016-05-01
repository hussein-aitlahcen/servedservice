﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ServedService
{
    public static class FastActivator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public delegate object DynamicCreationDelegate(object[] arguments);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="restrictedSkipVisibility"></param>
        /// <param name="returnType"></param>
        /// <param name="paramTypes"></param>
        /// <returns></returns>
        private static DynamicMethod MakeCreationMethodBoxed(bool restrictedSkipVisibility, Type returnType, params Type[] paramTypes)
        {
            var constructor = returnType.GetConstructor(paramTypes);

            if (constructor == null)
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                                  "Could not find constructor matching signature {0}({1})", returnType.FullName, string.Join(",", from argument in paramTypes
                                                                                                                                                                  select argument.FullName)));
            var constructorParams = constructor.GetParameters();
            var method =
                new DynamicMethod(
                    string.Format("{0}__{1}", constructor.DeclaringType.Name, Guid.NewGuid().ToString().Replace("-", "")), typeof(object),
                    new[] { typeof(object[]) }, restrictedSkipVisibility);

            ILGenerator gen = method.GetILGenerator();
            gen.Emit(OpCodes.Nop);
            for (int i = 0; i < constructorParams.Length; i++)
            {
                Type paramType = constructorParams[i].ParameterType;

                gen.Emit(OpCodes.Ldarg_0);
                switch (i)
                {
                    case 0:
                        gen.Emit(OpCodes.Ldc_I4_0);
                        break;

                    case 1:
                        gen.Emit(OpCodes.Ldc_I4_1);
                        break;

                    case 2:
                        gen.Emit(OpCodes.Ldc_I4_2);
                        break;

                    case 3:
                        gen.Emit(OpCodes.Ldc_I4_3);
                        break;

                    case 4:
                        gen.Emit(OpCodes.Ldc_I4_4);
                        break;

                    case 5:
                        gen.Emit(OpCodes.Ldc_I4_5);
                        break;

                    case 6:
                        gen.Emit(OpCodes.Ldc_I4_6);
                        break;

                    case 7:
                        gen.Emit(OpCodes.Ldc_I4_7);
                        break;

                    case 8:
                        gen.Emit(OpCodes.Ldc_I4_8);
                        break;

                    default:
                        gen.Emit(OpCodes.Ldc_I4_S, i);
                        break;
                }

                gen.Emit(OpCodes.Ldelem_Ref);
                gen.Emit(paramType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, paramType);
            }

            gen.Emit(OpCodes.Newobj, constructor);
            gen.Emit(OpCodes.Ret);

            return method;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="restrictedSkipVisibility"></param>
        /// <param name="returnType"></param>
        /// <param name="paramTypes"></param>
        /// <returns></returns>
        private static DynamicMethod MakeCreationMethodTypeSafe(bool restrictedSkipVisibility, Type returnType, params Type[] paramTypes)
        {
            var constructor = returnType.GetConstructor(paramTypes);

            if (constructor == null)
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                                  "Could not find constructor matching signature {0}({1})", returnType.FullName, string.Join(",", from argument in paramTypes
                                                                                                                                                                  select argument.FullName)));
            var constructorParams = constructor.GetParameters();

            var method =
                new DynamicMethod(
                    string.Format("{0}__{1}", constructor.DeclaringType.Name, Guid.NewGuid().ToString().Replace("-", "")), constructor.DeclaringType,
                    (from param in constructorParams select param.ParameterType).ToArray(), restrictedSkipVisibility);

            ILGenerator gen = method.GetILGenerator();
            for (int i = 0; i < constructorParams.Length; i++)
                if (i < 4)
                    switch (i)
                    {
                        case 0:
                            gen.Emit(OpCodes.Ldarg_0);
                            break;
                        case 1:
                            gen.Emit(OpCodes.Ldarg_1);
                            break;
                        case 2:
                            gen.Emit(OpCodes.Ldarg_2);
                            break;
                        case 3:
                            gen.Emit(OpCodes.Ldarg_3);
                            break;
                    }
                else
                    gen.Emit(OpCodes.Ldarg_S, i);  // Only up to 255 args

            gen.Emit(OpCodes.Newobj, constructor);
            gen.Emit(OpCodes.Ret);

            return method;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="returnType"></param>
        /// <param name="paramTypes"></param>
        /// <returns></returns>
        public static T GenerateDelegate<T>(Type returnType, params Type[] paramTypes)
        {
            return GenerateDelegate<T>(false, returnType, paramTypes);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="restrictedSkipVisibility"></param>
        /// <param name="returnType"></param>
        /// <param name="paramTypes"></param>
        /// <returns></returns>
        public static T GenerateDelegate<T>(bool restrictedSkipVisibility, Type returnType, params Type[] paramTypes)
        {
            var constructor = returnType.GetConstructor(paramTypes);
            if (constructor == null)
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                                  "Could not find constructor matching signature {0}({1})",
                                                                  returnType.FullName,
                                                                  string.Join(",", from param in paramTypes
                                                                                   select param.FullName)));

            var creator = MakeCreationMethodTypeSafe(restrictedSkipVisibility, returnType, paramTypes);
            return (T)(object)creator.CreateDelegate(typeof(T));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Func<T> GenerateDelegate<T>(Type type)
        {
            var creator = MakeCreationMethodTypeSafe(false, type, new Type[] { });
            return (Func<T>)creator.CreateDelegate(typeof(Func<T>));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GenerateFunc<T>()
            where T : class
        {
            return GenerateFunc<T>(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="restrictedSkipVisibility"></param>
        /// <returns></returns>
        public static T GenerateFunc<T>(bool restrictedSkipVisibility)
            where T : class
        {
            var delegateType = typeof(T);
            if (!typeof(Delegate).IsAssignableFrom(delegateType))
                throw new ArgumentException(string.Format("{0} is not a delegate type", delegateType.FullName));

            // Validate the delegate return type
            MethodInfo delMethod = delegateType.GetMethod("Invoke");
            var parameters = delMethod.GetParameters();
            var returnType = delMethod.ReturnType;
            if (returnType == typeof(void))
                throw new InvalidOperationException("Cannot register a delegate that doesn't return anything!");

            var paramTypes = (from parameter in parameters select parameter.ParameterType).ToArray();
            var constructor = returnType.GetConstructor(paramTypes);
            if (constructor == null)
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                                                                  "Could not find constructor matching signature {0}({1})",
                                                                  returnType.FullName,
                                                                  string.Join(",", from param in paramTypes
                                                                                   select param.FullName)));

            if (delMethod.ReturnType != constructor.DeclaringType)
                throw new InvalidOperationException("The return type of the delegate must match the constructors declaring type");

            // Validate the signatures
            ParameterInfo[] delParams = delMethod.GetParameters();
            ParameterInfo[] constructorParam = constructor.GetParameters();
            if (delParams.Length != constructorParam.Length)
                throw new InvalidOperationException("The delegate signature does not match that of the constructor");

            var method = MakeCreationMethodTypeSafe(restrictedSkipVisibility, returnType, paramTypes);
            return method.CreateDelegate(delegateType) as T;
        }
    }
}
