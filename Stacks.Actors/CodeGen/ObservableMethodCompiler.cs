﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Stacks.Actors.CodeGen
{
    public class ObservableMethodCompiler : IActorCompilerStrategy
    {
        public bool CanCompile(MethodInfoMapping method)
        {
            var retType = method.InterfaceInfo.ReturnType;
            return retType.IsGenericType && retType.GetGenericTypeDefinition() == typeof(IObservable<>);
        }

        public bool CanCompile(PropertyInfoMapping property)
        {
            return false;
        }

        public void Implement(MethodInfoMapping method, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            var mi = method.InterfaceInfo;

            var mBuilder = wrapperBuilder.DefineMethod(method.PublicName,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                CallingConventions.HasThis, mi.ReturnType,
                mi.GetParameters().Select(p => p.ParameterType).ToArray());
            var mParams = mi.GetParameters();

            for (var i = 1; i <= mParams.Length; ++i)
            {
                mBuilder.DefineParameter(i, ParameterAttributes.None, mParams[i - 1].Name);
            }

            var il = mBuilder.GetILGenerator();
            // ((actorInterface)base.actorImplementation).method(params...)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld,
                    typeof (ActorWrapperBase).GetField("actorImplementation",
                        BindingFlags.Instance | BindingFlags.NonPublic));
                il.Emit(OpCodes.Castclass, actorInterface);
                for (var i = 1; i <= mi.GetParameters().Length; ++i)
                {
                    il.Emit(OpCodes.Ldarg, i);
                }
                il.EmitCall(OpCodes.Call, mi, null);
            }

            //il.EmitCall(OpCodes.Call);

            il.Emit(OpCodes.Ret);
        }

        public void Implement(PropertyInfoMapping property, Type actorInterface, TypeBuilder wrapperBuilder)
        {
            throw new NotSupportedException();
        }
    }
}
