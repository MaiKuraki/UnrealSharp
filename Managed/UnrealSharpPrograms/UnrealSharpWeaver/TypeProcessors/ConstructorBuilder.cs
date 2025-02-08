﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using UnrealSharpWeaver.MetaData;

namespace UnrealSharpWeaver.TypeProcessors;

public static class ConstructorBuilder
{
    public static MethodDefinition MakeStaticConstructor(TypeDefinition type)
    {
        return CreateStaticConstructor(type, MethodAttributes.Static);
    }

    public static MethodDefinition CreateStaticConstructor(TypeDefinition type, MethodAttributes attributes, params TypeReference[] parameterTypes)
    {
        MethodDefinition staticConstructor = type.GetStaticConstructor();

        if (staticConstructor == null)
        {
            staticConstructor = WeaverHelper.AddMethodToType(type, ".cctor", 
                WeaverHelper.VoidTypeRef,
                attributes | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
                parameterTypes);
        }
        else
        {
            // Remove the return instruction from existing static constructor
            WeaverHelper.RemoveReturnInstruction(staticConstructor);
        }
        
        return staticConstructor;
    }
    
    public static MethodDefinition CreateConstructor(TypeDefinition typeDefinition, MethodAttributes attributes, params TypeReference[] parameterTypes)
    {
        MethodDefinition? constructor = typeDefinition.GetConstructors().FirstOrDefault(ctor => ctor.Parameters.Count == parameterTypes.Length);
        
        if (constructor == null)
        {
            constructor = WeaverHelper.AddMethodToType(typeDefinition, ".ctor", 
                WeaverHelper.VoidTypeRef,
                attributes | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                parameterTypes);
        }
        
        return constructor;
    }
    
    public static void CreateTypeInitializer(TypeDefinition typeDefinition, Instruction field, Instruction[] initializeInstructions, string engineName = "", bool finalizeMethod = false)
    {
        MethodDefinition staticConstructorMethod = MakeStaticConstructor(typeDefinition);
        ILProcessor processor = staticConstructorMethod.Body.GetILProcessor();
        
        // typeof(TypeDefinition).GetAssemblyName()
        processor.Emit(OpCodes.Ldtoken, typeDefinition);
        processor.Emit(OpCodes.Call, WeaverHelper.GetTypeFromHandleMethod);
        processor.Emit(OpCodes.Call, WeaverHelper.GetAssemblyNameMethod); 
        
        engineName = string.IsNullOrEmpty(engineName) ? WeaverHelper.GetEngineName(typeDefinition) : engineName;
        processor.Emit(OpCodes.Ldstr, engineName);
        
        foreach (Instruction instruction in initializeInstructions)
        {
            processor.Append(instruction);
        }
        
        processor.Append(field);
        
        if (finalizeMethod)
        {
            staticConstructorMethod.FinalizeMethod();
        }
    }
    
    public static void VerifySingleResult<T>(T[] results, TypeDefinition type, string endMessage)
    {
        switch (results.Length)
        {
            case 0:
                throw new RewriteException(type, $"Could not find {endMessage}");
            case > 1:
                throw new RewriteException(type, $"Found more than one {endMessage}");
        }
    }

    public static void InitializeFields(MethodDefinition staticConstructor, List<PropertyMetaData> fields, Instruction loadNativeClassField)
    {
        ILProcessor processor = staticConstructor.Body.GetILProcessor();
        foreach (var property in fields)
        {
            Instruction loadNativeProperty;
            Instruction setNativeProperty;
            if (property.NativePropertyField == null)
            {
                VariableDefinition nativePropertyVar = WeaverHelper.AddVariableToMethod(processor.Body.Method, WeaverHelper.IntPtrType);
                loadNativeProperty = Instruction.Create(OpCodes.Ldloc, nativePropertyVar);
                setNativeProperty = Instruction.Create(OpCodes.Stloc, nativePropertyVar);
            }
            else
            {
                loadNativeProperty = Instruction.Create(OpCodes.Ldsfld, property.NativePropertyField);
                setNativeProperty = Instruction.Create(OpCodes.Stsfld, property.NativePropertyField);
            }
            
            property.InitializePropertyPointers(processor, loadNativeClassField, setNativeProperty);
            property.InitializePropertyOffsets(processor, loadNativeProperty);
            property.PropertyDataType.WritePostInitialization(processor, property, loadNativeProperty, setNativeProperty);
        }
    }
}