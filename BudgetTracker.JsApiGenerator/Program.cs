﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BudgetTracker.JsModel.Attributes;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BudgetTracker.JsApiGenerator
{
    class Program
    {
        private static string _fileName;

        static void Main(string[] args)
        {
            _fileName = string.Join(Path.DirectorySeparatorChar,
                new[] {@"..", "BudgetTracker.Client", "src", "generated-types.ts"});

            _fileName = Path.GetFullPath(_fileName);
            Console.WriteLine("Generating rest client to {0}", _fileName);
            
            File.WriteAllText(_fileName, @"// autogenerated
import rest from './services/Rest';

");
            
            var types = typeof(Startup).Assembly.GetTypes();
            var exportableTypes = types.Where(v => v.GetCustomAttribute<ExportJsModelAttribute>() != null).ToList();

            foreach (var type in types.Where(v => v.IsEnum).OrderBy(v => v.Name))
            {
                GenerateEnum(type);
            }
            
            foreach (var type in exportableTypes.OrderBy(v=>v.Name))
            {
                GenerateType(type, exportableTypes);
            }

            var controllers = types.Where(v => typeof(Controller).IsAssignableFrom(v) && v.GetCustomAttribute<HideFromRestAttribute>() == null && !v.IsAbstract).ToList();
            foreach (var controller in controllers.OrderBy(v=>v.Name))
            {
                GenerateController(controller, exportableTypes);
            }
        }

        private static void GenerateEnum(Type type)
        {
            using(var fileWriter = File.AppendText(_fileName))
            {
                fileWriter.Write($"export class {type.Name}Enum {{\n");

                var staticMembers = "";
                var getName = "    getName() {\n";
                var getLabel = "    getLabel() {\n";
                var getItems = "    static getEnums() {\n";
                getItems += "        return [\n";
                
                foreach (var v in Enum.GetValues(type))
                {
                    getName += $"        if (this.id === {(int)v}) return \"{v}\";\n";
                    getLabel += $"        if (this.id === {(int)v}) return \"{v.GetDisplayName()}\";\n";
                    staticMembers += $"    static {v} = new {type.Name}Enum({(int) v});\n";
                    getItems += $"            {type.Name}Enum.{v},\n";
                }

                getItems = getItems.TrimEnd(',', '\n');
                getItems += "\n        ];\n";

                getName += "        return this.id;\n";
                getLabel += "        return this.id;\n";
                getName += "    }\n";
                getLabel += "    }\n";
                getItems += "    }\n";

                    
                fileWriter.Write(staticMembers);
                fileWriter.WriteLine(getItems);
                fileWriter.WriteLine($"    id: number;\n\n" +
                                     $"    constructor(id: number) {{\n" +
                                     $"        this.id = id;\n" +
                                     $"    }}\n\n" +
                                     $"    getId(): number {{ return this.id; }}\n");

                fileWriter.WriteLine(getName);
                fileWriter.WriteLine(getLabel);
                fileWriter.WriteLine("}\n");
            }
        }

        private static void GenerateController(Type type, IEnumerable<Type> knownTypes)
        {
            var controllerName = type.Name;
            if (controllerName.EndsWith("Controller"))
            {
                controllerName = controllerName.Substring(0, controllerName.IndexOf("Controller"));
            }

            var whiteList = new[] {typeof(OkResult), typeof(StatusCodeResult)};

            var methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(v => v.GetCustomAttribute<HideFromRestAttribute>() == null && !v.DeclaringType.IsAssignableFrom(typeof(Controller)))
                .OrderBy(v=>v.Name)
                .ToList();
            
            var methods = methodInfos
                .Where(v => !typeof(IActionResult).IsAssignableFrom(ExpandType(v.ReturnType)) || whiteList.Contains(ExpandType(v.ReturnType)))
                .GroupBy(v => v.Name)
                .Select(v => v.OrderByDescending(s => s.GetParameters().Length).First())
                .ToList();
            
            var navigations = methodInfos
                .Where(v => typeof(IActionResult).IsAssignableFrom(ExpandType(v.ReturnType)) && !whiteList.Contains(ExpandType(v.ReturnType)))
                .Where(v => v.GetParameters().Length == 0)
                .ToList(); 

            if (!methods.Any() && !navigations.Any())
                return;
            
            using (var fileWrite = File.AppendText(_fileName))
            {
                fileWrite.Write($"export class {type.Name} {{\n");

                foreach (var method in methods)
                {
                    var jsMethod = "get";

                    if (method.GetCustomAttribute<HttpPostAttribute>() != null)
                    {
                        jsMethod = "post";
                    }

                    bool hasResponse = GetTypescriptType(method.ReturnType, knownTypes) != "void";

                    bool isBoolResponse = ExpandType(method.ReturnType) == typeof(bool);

                    var boolPrefix = isBoolResponse ? "\"true\" == await " : "";
                    
                    if (jsMethod == "get")
                    {
                        fileWrite.WriteLine($"    static async {GetMethodSignature(method)}: Promise<{GetTypescriptType(method.ReturnType, knownTypes)}> {{ ");
                        fileWrite.WriteLine($"      return {boolPrefix}rest.{jsMethod}({GetMethodQuery(method, controllerName)}, {hasResponse.ToString().ToLower()}, {ShouldDeserialize(method.ReturnType).ToString().ToLower()}); ");
                        fileWrite.WriteLine("    };");
                    }
                    else
                    {
                        var endpoint = $"`/{controllerName}/{method.Name}`";
                        
                        fileWrite.WriteLine($"    static async {GetMethodSignature(method, knownTypes)}: Promise<{GetTypescriptType(method.ReturnType, knownTypes)}> {{ ");
                        var enumerable = method.GetParameters().Select(v => new { original = v, filtered = FilterKeywords(v.Name)}).ToList();
                        fileWrite.WriteLine($"      let data = {{");
                        foreach (var v in enumerable)
                        {
                            fileWrite.WriteLine($"          {v.original.Name}: {v.filtered},");
                        }
                        fileWrite.WriteLine($"      }}");
                        fileWrite.WriteLine($"      return {boolPrefix}rest.{jsMethod}({endpoint}, data, {hasResponse.ToString().ToLower()}, {ShouldDeserialize(method.ReturnType).ToString().ToLower()}); ");
                        fileWrite.WriteLine("    };");
                    }
                }

                if (navigations.Any() && methods.Any())
                {
                    fileWrite.WriteLine();
                }
                
                foreach (var navigation in navigations)
                {
                    fileWrite.WriteLine($"    static {CamelCase(FilterKeywords(navigation.Name))} = `/{controllerName}/{navigation.Name}`");
                }

                fileWrite.Write("}\n\n");
            }
        }

        private static bool ShouldDeserialize(Type type)
        {
            type = ExpandType(type);
            return !type.IsPrimitive && type != typeof(string);
        }
        
        private static string GetMethodSignature(MethodInfo method, IEnumerable<Type> knownTypes = null)
        {
            var args = method.GetParameters().Select(v => FilterKeywords(v.Name) + ": " + GetTypescriptType(v.ParameterType, knownTypes))
                .Join(", ");
            return $"{CamelCase(method.Name)}({args})";
        }

        private static string FilterKeywords(string argName)
        {
            switch (argName)
            {   
                case "function":
                    return "function2";
                default:
                    return argName;
            }
        }

        private static string GetMethodQuery(MethodInfo method, string controllerName)
        {
            var endpoint = $"`/{controllerName}/{method.Name}";
            var param = method.GetParameters();
            if (param.Length > 0)
            {
                endpoint += "?";
                endpoint += param.Select(v =>
                {
                    var value = GetTypescriptType(v.ParameterType) == "string" ? "encodeURIComponent(" + FilterKeywords(v.Name) + ")" : FilterKeywords(v.Name); 
                    return v.Name + "=" + "` + " + value + " + `";
                }).Join("&");
            }
            
            endpoint +="`";

            if (endpoint.EndsWith(" + ``"))
            {
                endpoint = endpoint.Substring(0, endpoint.LastIndexOf(" + ``"));
            }
            
            return endpoint;
        }

        private static void GenerateType(Type type, IEnumerable<Type> knownTypes)
        {
            Console.WriteLine($"Exporting {type.Name}...");
            using (var fileWrite = File.AppendText(_fileName))
            {
                var extends = "";
                if (type.BaseType != typeof(object))
                {
                    extends = "extends " + type.BaseType.Name;
                }
                
                fileWrite.Write($"export interface {type.Name} {extends}{{\n");

                foreach (var p in type.GetProperties().OrderBy(v=>v.Name))
                {
                    try
                    {
                        if (p.DeclaringType != type)
                            continue;
                        
                        var getMethod = p.GetGetMethod(false);
                        if (getMethod.GetBaseDefinition() != getMethod)
                            continue;
                        
                        if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                            continue;
                        
                        var formattableString = $"   {CamelCase(p.Name)}: {GetTypescriptType(ExpandType(p.PropertyType), knownTypes)};";
                        fileWrite.WriteLine(formattableString);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                
                fileWrite.Write("}\n\n");
                
            }
        }

        private static string CamelCase(string pName)
        {
            var sb = new StringBuilder(pName);
            sb[0] = sb[0].ToString().ToLower()[0];
            return sb.ToString();
        }

        private static Type ExpandType(Type type)
        {
            if (typeof(Task).IsAssignableFrom(type) && type.IsGenericType)
            {
                return type.GetGenericArguments()[0];
            }

            if (typeof(Nullable).IsAssignableFrom(type) && type.IsGenericType)
            {
                return type.GetGenericArguments()[0];
            }

            if (Nullable.GetUnderlyingType(type) != null)
            {
                return Nullable.GetUnderlyingType(type);
            }

            return type;
        } 
        
      private static string GetTypescriptType(Type type, IEnumerable<Type> knownTypes = null, [CallerLineNumber] int ln = 0, [CallerMemberName] string who = null)
        {
            type = ExpandType(type);
            
            bool array = false;

            if (typeof(IDictionary<,>).IsSubTypeOfRawGeneric(type))
            {
                return "any";
                var ga = type.GetGenericArguments();
                return "Map<" + GetTypescriptType(ga[0]) + "," + GetTypescriptType(ga[1]) + ">";
            }
            
            if (typeof(IEnumerable<>).IsSubTypeOfRawGeneric(type) && type != typeof(string))
            {
                type = type.GetGenericArguments()[0];
                array = true;
            }

            string result = "";
            
            if (knownTypes?.Contains(type) == true)
                result = type.Name;
            else if (type == typeof(String) || type == typeof(Guid) || type.IsEnum)
                result = "string";
            else if (type == typeof(int))
                result = "number";
            else if (type == typeof(double))
                result = "number";
            else if (type == typeof(bool))
                result = "boolean";
            else if (type == typeof(DateTime))
                result = "string";
            else if (type == typeof(OkResult) || type == typeof(StatusCodeResult))
            {
                result = "void";
            } else {

                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Error.WriteLine($"Unsupported type {type.FullName} at {who}:{ln}");
                Console.ForegroundColor = oldColor;
                result = "any";
            }

            if (array)
            {
                result += "[]";
            }

            return result;

        }
    }

    internal static class Helpers
    {
        public static bool IsSubTypeOfRawGeneric(this Type toCheck, Type type)
        {
            if (toCheck.IsInterface)
            {
                if (toCheck.IsInterface && toCheck.GetGenericTypeDefinition() == type)
                    return true;

                var interfaceTypes = type.GetInterfaces().ToList();
                if (type.IsInterface)
                {
                    interfaceTypes.Add(type);
                }
                foreach (var interfaceType in interfaceTypes)
                {
                    var current = interfaceType.GetTypeInfo().IsGenericType
                        ? interfaceType.GetGenericTypeDefinition()
                        : interfaceType;

                    if (current == toCheck)
                        return true;
                }
                
                return false;
            }

            while (type != null && type != typeof(object)) {
                var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (toCheck == cur) {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }
    }
}