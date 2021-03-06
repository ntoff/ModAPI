﻿/*  
 *  ModAPI
 *  Copyright (C) 2015 FluffyFish / Philipp Mohrenstecher
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *  
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *  
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *  
 *  To contact me you can e-mail me at info@fluffyfish.de
 */

/**
 * Contributing Authors:
 * magomerdino | Added a fix to add parameters to resolved methods.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ModAPI.Utils
{
    /// <summary>
    /// Thie MonoHelper is providing methods to resolve method and type references
    /// within a managed code library to make it easier modifying them.
    /// </summary>
    public class MonoHelper
    {

        /// <summary>
        /// CopyField copies a field definition into a new copy which can be added
        /// to another module.
        /// </summary>
        /// <param name="field">The FieldDefinition to copy</param>
        /// <returns>A copy of the FieldDefinition</returns>
        public static FieldDefinition CopyField(FieldDefinition field)
        {
            FieldDefinition newField = new FieldDefinition(field.Name, field.Attributes, field.FieldType);
            newField.HasDefault = field.HasDefault;
            if (field.InitialValue != null)
            {
                byte[] arr = new byte[field.InitialValue.Length];
                field.InitialValue.CopyTo(arr, 0);
                newField.InitialValue = arr;
            }
            return newField;
        }

        /// <summary>
        /// CopyMethod copies a method definition into a new copy which can be added
        /// to another module.
        /// </summary>
        /// <param name="method">The MethodDefinition to copy</param>
        /// <returns>A copy of the MethodDefinition</returns>
        public static MethodDefinition CopyMethod(MethodDefinition method) 
        {
            MethodDefinition newMethod = new MethodDefinition(method.Name, method.Attributes, method.ReturnType);
            foreach (ParameterDefinition param in method.Parameters) 
            {
                ParameterDefinition newParam = new ParameterDefinition(param.Name, param.Attributes, param.ParameterType);
                newMethod.Parameters.Add(newParam);
            }

            foreach (CustomAttribute attr in method.CustomAttributes)
            {
                CustomAttribute newAttr = new CustomAttribute(attr.Constructor);
                foreach (CustomAttributeArgument arg in attr.ConstructorArguments)
                {
                    CustomAttributeArgument newArg = new CustomAttributeArgument(arg.Type, arg.Value);
                    newAttr.ConstructorArguments.Add(newArg);
                }
                foreach (CustomAttributeNamedArgument arg in attr.Fields)
                {
                    CustomAttributeNamedArgument newArg = new CustomAttributeNamedArgument(arg.Name, new CustomAttributeArgument(arg.Argument.Type, arg.Argument.Value));
                    newAttr.Fields.Add(newArg);
                }
                newMethod.CustomAttributes.Add(newAttr);
            }

            if (method.Body != null) 
            {
                if (newMethod.Body == null)
                    newMethod.Body = new MethodBody(newMethod);
                foreach (Instruction inst in method.Body.Instructions) 
                {
                    newMethod.Body.Instructions.Add(inst);
                }

                foreach (VariableDefinition var in method.Body.Variables) 
                {
                    VariableDefinition newVar = new VariableDefinition(var.Name, var.VariableType);
                    newMethod.Body.Variables.Add(newVar);
                }

                foreach (ExceptionHandler handler in method.Body.ExceptionHandlers)
                {
                    ExceptionHandler newHandler = new ExceptionHandler(handler.HandlerType);
                    newHandler.HandlerStart = handler.HandlerStart;
                    newHandler.HandlerEnd = handler.HandlerEnd;
                    newHandler.TryStart = handler.TryStart;
                    newHandler.TryEnd = handler.TryEnd;
                    newHandler.FilterStart = handler.FilterStart;
                    newHandler.CatchType = handler.CatchType;
                    newMethod.Body.ExceptionHandlers.Add(newHandler);
                }

                newMethod.Body.InitLocals = method.Body.InitLocals;
            }
            return newMethod;
        }

        /// <summary>
        /// Remap changes the IL code and make all references provided link to the
        /// method definitions provided.
        /// </summary>
        /// <param name="hostModule">The module in which the changes are made.</param>
        /// <param name="method">The method in which the references should be remapped.</param>
        /// <param name="NewMethods">A dictionary which describes which method references should be remapped to which new method definitions.</param>
        public static void Remap(
            ModuleDefinition hostModule,
            MethodDefinition method,
            Dictionary<MethodReference, MethodDefinition> NewMethods)
        {
            if (method.Body != null)
            {
                foreach (Instruction instruction in method.Body.Instructions)
                {
                    if (instruction.Operand is MethodReference)
                    {
                        MethodReference methodReference = (MethodReference)instruction.Operand;
                        if (NewMethods.ContainsKey(methodReference))
                            instruction.Operand = (MethodReference)NewMethods[methodReference]; //hostModule.Import(
                    }
                }
            }
        }

        /// <summary>
        /// This method resolves the field type of a field definition.
        /// </summary>
        /// <param name="hostModule">The module in which the changes are made.</param>
        /// <param name="field">The field definition to resolve.</param>
        /// <param name="AddedClasses">Newly added types to lookup while resolving.</param>
        /// <param name="TypesMap">A map of types to lookup while resolving.</param>
        public static void Resolve(
            ModuleDefinition hostModule,
            FieldDefinition field,
            Dictionary<TypeReference, TypeDefinition> AddedClasses,
            Dictionary<TypeReference, TypeReference> TypesMap)
        {
            field.FieldType = Resolve(hostModule, field.FieldType, AddedClasses, TypesMap);
        }

        /// <summary>
        /// This method resolves all references within a method including the IL Code.
        /// </summary>
        /// <param name="hostModule">The module in which the changes are made.</param>
        /// <param name="method">The method to resolve.</param>
        /// <param name="AddedClasses">Newly added types to lookup while resolving.</param>
        /// <param name="AddedFields">Newly added fields to lookup while resolving.</param>
        /// <param name="AddedMethods">Newly added methods to lookup while resolving.</param>
        /// <param name="InjectedMethods">Injected methods to lookup while resolving.</param>
        /// <param name="TypesMap">A map of types to lookup while resolving.</param>
        public static void Resolve(
            ModuleDefinition hostModule,
            MethodDefinition method,
            Dictionary<TypeReference, TypeDefinition> AddedClasses,
            Dictionary<FieldReference, FieldDefinition> AddedFields,
            Dictionary<MethodReference, MethodDefinition> AddedMethods,
            Dictionary<MethodReference, MethodDefinition> InjectedMethods,
            Dictionary<TypeReference, TypeReference> TypesMap)
        {
            /*
	         * Fix from magomerdino
	         * http://www.modapi.de/index.php/User/314-magomerdino/
	         * http://www.modapi.de/index.php/Thread/89-Little-Fix-New-mods/?postID=525#post525
	         * Posted on 08/23/2015
	         */
            foreach (ParameterDefinition param in method.Parameters)
            {
                param.ParameterType = Resolve(hostModule, param.ParameterType, AddedClasses, TypesMap);
            }
            /* End of fix */
            foreach (CustomAttribute attr in method.CustomAttributes)
            {
                if (attr.Constructor.Module != hostModule)
                    attr.Constructor = hostModule.Import(attr.Constructor);
                for (int i = 0; i < attr.ConstructorArguments.Count; i++)
                {
                    CustomAttributeArgument arg = attr.ConstructorArguments[i];
                    if (arg.Type.Module != hostModule)
                    {
                        attr.ConstructorArguments[i] = new CustomAttributeArgument(hostModule.Import(arg.Type), arg.Value);
                    }
                } 
                for (int i = 0; i < attr.Fields.Count; i++)
                {
                    CustomAttributeNamedArgument arg = attr.Fields[i];
                    if (arg.Argument.Type.Module != hostModule)
                    {
                        attr.Fields[i] = new CustomAttributeNamedArgument(arg.Name, new CustomAttributeArgument(hostModule.Import(arg.Argument.Type), arg.Argument.Value));
                    }
                }
            }
            if (method.Body != null)
            {
                foreach (ExceptionHandler handler in method.Body.ExceptionHandlers)
                {
                    handler.CatchType = Resolve(hostModule, handler.CatchType, AddedClasses, TypesMap);
                }
                foreach (VariableDefinition variable in method.Body.Variables)
                {
                    variable.VariableType = Resolve(hostModule, variable.VariableType, AddedClasses, TypesMap);
                }
                foreach (Instruction instruction in method.Body.Instructions)
                {
                    if (instruction.Operand is GenericInstanceMethod)
                    {
                        GenericInstanceMethod genericInstance = (GenericInstanceMethod)instruction.Operand;
                        instruction.Operand = Resolve(hostModule, genericInstance, AddedClasses, AddedMethods, TypesMap);
                    }
                    else if (instruction.Operand is MethodReference)
                    {
                        MethodReference methodReference = (MethodReference)instruction.Operand;
                        instruction.Operand = Resolve(hostModule, methodReference, AddedClasses, AddedMethods, TypesMap);
                    }
                    else if (instruction.Operand is TypeReference)
                    {
                        TypeReference typeReference = (TypeReference)instruction.Operand;
                        instruction.Operand = Resolve(hostModule, typeReference, AddedClasses, TypesMap);
                    }
                    else if (instruction.Operand is FieldReference)
                    {
                        FieldReference fieldReference = (FieldReference)instruction.Operand;
                        instruction.Operand = Resolve(hostModule, fieldReference, AddedClasses, AddedFields, TypesMap);
                    }
                    
                }
            }
        }

        /// <summary>
        /// This method resolves a type. This method ignores the methods and fields. You have to
        /// resolve them manually.
        /// </summary>
        /// <param name="hostModule">The module in which the changes are made.</param>
        /// <param name="type">The type to resolve.</param>
        /// <param name="AddedClasses">Newly added classes to lookup while resolving.</param>
        /// <param name="TypesMap">A map of types to lookup while resolving.</param>
        /// <returns></returns>
        protected static TypeReference Resolve(
            ModuleDefinition hostModule,
            TypeReference type,
            Dictionary<TypeReference, TypeDefinition> AddedClasses,
            Dictionary<TypeReference, TypeReference> TypesMap)
        {
            if (type is GenericInstanceType)
            {
                GenericInstanceType gType = (GenericInstanceType)type;
                GenericInstanceType nType = new GenericInstanceType(Resolve(hostModule, gType.ElementType, AddedClasses, TypesMap));
                foreach (TypeReference t in gType.GenericArguments)
                {
                    nType.GenericArguments.Add(Resolve(hostModule, t, AddedClasses, TypesMap));
                }
                return nType;
            }
            if (type == null || type is GenericParameter || (type.IsArray && type.GetElementType() is GenericParameter))
                return type;
            if (TypesMap.ContainsKey(type))
                return hostModule.Import(TypesMap[type]);
            foreach (TypeReference addedType in AddedClasses.Keys)
            {
                if (addedType == type)
                {
                    return hostModule.Import(AddedClasses[addedType]);
                }
            }
            if (type.Module != hostModule)
            {
                TypeDefinition t = hostModule.GetType(type.FullName);
                if (t != null)
                {
                    return (TypeReference)t;
                }
                if (hostModule == null || type == null)
                    return type;
                else
                {
                    try
                    {
                        return hostModule.Import(type);
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine(type.GetElementType());
                        System.Console.WriteLine(type.GetType().FullName);
                        throw e;
                    }
                }
            }
            else
                return type;
                
        }

        /// <summary>
        /// This method resolves a field reference.
        /// </summary>
        /// <param name="hostModule">The module in which the changes are made.</param>
        /// <param name="field">The field reference to resolve.</param>
        /// <param name="AddedClasses">Newly added types to lookup while resolving.</param>
        /// <param name="AddedFields">Newly added fields to lookup while resolving.</param>
        /// <param name="TypesMap">A map of types to lookup while resolving.</param>
        /// <returns>The resolved FieldReference</returns>
        protected static FieldReference Resolve(
            ModuleDefinition hostModule,
            FieldReference field,
            Dictionary<TypeReference, TypeDefinition> AddedClasses,
            Dictionary<FieldReference, FieldDefinition> AddedFields,
            Dictionary<TypeReference, TypeReference> TypesMap)
        {
            foreach (FieldReference addedField in AddedFields.Keys)
            {
                if (addedField.FullName == field.FullName || addedField == field)
                {
                    if (AddedFields[addedField].Module != hostModule)
                        return hostModule.Import(AddedFields[addedField]);
                    else 
                        return AddedFields[addedField]; //hostModule.Import(
                }
            }
            if (field.Module != hostModule)
            {
                TypeDefinition t = hostModule.GetType(field.DeclaringType.FullName);
                if (t != null)
                {
                    foreach (FieldDefinition f in t.Fields)
                    {
                        if (f.FullName == field.FullName)
                        {
                            return (FieldReference)f;
                        }
                    }
                }
                return hostModule.Import(field);
            }
            else
            {
                return field;
            }
        }

        /// <summary>
        /// Resolves a method reference. Because of the nature of method references
        /// this method does not resolve the IL code.
        /// </summary>
        /// <param name="hostModule">The module in which the changes are made.</param>
        /// <param name="method">The method reference to resolve.</param>
        /// <param name="AddedClasses">Newly added types to lookup while resolving.</param>
        /// <param name="AddedMethods">Newly added methods to lookup while resolving.</param>
        /// <param name="TypesMap">A map of types to lookup while resolving.</param>
        /// <returns>The resolved method reference.</returns>
        protected static MethodReference Resolve(
            ModuleDefinition hostModule,
            MethodReference method,
            Dictionary<TypeReference, TypeDefinition> AddedClasses,
            Dictionary<MethodReference, MethodDefinition> AddedMethods,
            Dictionary<TypeReference, TypeReference> TypesMap)
        {
            if (AddedMethods.ContainsKey(method))
                return AddedMethods[method]; //hostModule.Import(
            MethodReference newReference = new MethodReference(method.Name, Resolve(hostModule, method.ReturnType, AddedClasses, TypesMap), Resolve(hostModule, method.DeclaringType, AddedClasses, TypesMap));
            
            foreach (GenericParameter generic in method.GenericParameters)
            {
                GenericParameter newGeneric = new GenericParameter(generic.Name, newReference);
                newGeneric.Attributes = generic.Attributes;

                newReference.GenericParameters.Add(newGeneric);
            }
            if (method.ReturnType is GenericParameter)
            {
                GenericParameter g = (GenericParameter)method.ReturnType;
                if (newReference.GenericParameters.Count > g.Position)
                    newReference.ReturnType = newReference.GenericParameters[g.Position];
            }
            foreach (ParameterDefinition parameter in method.Parameters)
                newReference.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, Resolve(hostModule, parameter.ParameterType, AddedClasses, TypesMap)));
            newReference.CallingConvention = method.CallingConvention;
            newReference.MethodReturnType.Attributes = method.MethodReturnType.Attributes;
            newReference.HasThis = method.HasThis;
            return newReference;
        }

        /// <summary>
        /// This method resolves a generic instance method.
        /// </summary>
        /// <param name="hostModule">The module in which the changes are made.</param>
        /// <param name="method">The generic instance method to resolve.</param>
        /// <param name="AddedClasses">Newly added types to lookup while resolving.</param>
        /// <param name="AddedMethods">Newly added methods to lookup while resolving.</param>
        /// <param name="TypesMap">A map of types to lookup while resolving.</param>
        /// <returns>The resolved generic instance method.</returns>
        protected static GenericInstanceMethod Resolve(
            ModuleDefinition hostModule,
            GenericInstanceMethod method,
            Dictionary<TypeReference, TypeDefinition> AddedClasses,
            Dictionary<MethodReference, MethodDefinition> AddedMethods,
            Dictionary<TypeReference, TypeReference> TypesMap)
        {
            if (AddedMethods.ContainsKey(method))
                return (GenericInstanceMethod)((MethodReference)AddedMethods[method]); //hostModule.Import(
            MethodReference elementMethod = Resolve(hostModule, method.ElementMethod, AddedClasses, AddedMethods, TypesMap);
            GenericInstanceMethod newReference = new GenericInstanceMethod(elementMethod);
            foreach (TypeReference type in method.GenericArguments)
            {
                TypeReference newType = Resolve(hostModule, type, AddedClasses, TypesMap);
                newReference.GenericArguments.Add(newType);
            }
            if (method.ReturnType is GenericParameter)
            {
                GenericParameter g = (GenericParameter)method.ReturnType;

                newReference.ReturnType = elementMethod.GenericParameters[g.Position];
            }
            return newReference;
        }

        /// <summary>
        /// This method parses custom attributes offered by the ModAPI to offer
        /// certain abilities like launching a mod ingame or inmenu.
        /// </summary>
        /// <remarks>
        /// This method adds new elements to the XDocument provided to it.
        /// This is because you cant scan efficently for all methods with a certain
        /// attribute. While in-game the ModAPI can use this information to determine
        /// which methods to call when.
        /// </remarks>
        /// <param name="mod">The mod currently parsed.</param>
        /// <param name="configuration">The configuration file where the attributes are saved for quicker lookup while in-game.</param>
        /// <param name="method">The method to parse</param>
        /// <param name="ConfigurationAttributes">A dictionary filled with the attributes to look for.</param>
        public static void ParseCustomAttributes(ModAPI.Data.Mod mod, XDocument configuration, MethodDefinition method, Dictionary<string, TypeDefinition> ConfigurationAttributes)
        {
            for (int k = 0; k < method.CustomAttributes.Count; k++)
            {
                CustomAttribute attribute = method.CustomAttributes[k];
                string attrKey = attribute.AttributeType.FullName;
                if (ConfigurationAttributes.ContainsKey(attrKey))
                {
                    TypeDefinition attributeType = ConfigurationAttributes[attrKey];
                    bool valid = true;
                    foreach (TypeReference interfc in attributeType.Interfaces) 
                    {
                        if (interfc.Name == "IStaticAttribute" && !method.IsStatic)
                        {
                            Debug.Log("Modloader: "+mod.Game.GameConfiguration.ID, "Method \"" + method.FullName + "\" is using attribute \"" + method.CustomAttributes[k].AttributeType.FullName + "\" which is only suitable for static methods but isn't marked as static.", Debug.Type.WARNING);
                            valid = false;
                        }
                        if (interfc.Name == "INoParametersAttribute" && method.Parameters.Count > 0)
                        {
                            Debug.Log("Modloader: " + mod.Game.GameConfiguration.ID, "Method \"" + method.FullName + "\" is using attribute \"" + method.CustomAttributes[k].AttributeType.FullName + "\" which is only suitable for methods without parameters but has parameters.", Debug.Type.WARNING);
                            valid = false;
                        }
                    }
                    if (!valid)
                        continue;
                    List<string> Names = new List<string>();
                    if (attribute.ConstructorArguments.Count > 0)
                    {
                        foreach (MethodDefinition m in attributeType.Methods)
                        {
                            if (m.IsConstructor)
                            {
                                foreach (ParameterDefinition p in m.Parameters)
                                {
                                    Names.Add(p.Name);
                                }
                                break;
                            }
                        }
                    }
                    XElement newElement = new XElement(attributeType.Name);
                    for (int i = 0; i < attribute.ConstructorArguments.Count; i++)
                    {
                        CustomAttributeArgument arg = attribute.ConstructorArguments[i];
                        newElement.SetAttributeValue(Names[i], arg.Value);
                    }
                    newElement.Value = method.FullName;
                    newElement.SetAttributeValue("ModID", mod.ID);
                    configuration.Root.Add(newElement);

                    method.CustomAttributes.RemoveAt(k);
                    k--;
                }
            }
        }
    }
}
