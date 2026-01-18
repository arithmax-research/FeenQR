#!/usr/bin/env dotnet script
#r "nuget: Microsoft.SemanticKernel.Connectors.Qdrant, 1.68.0-preview"
#r "nuget: Qdrant.Client, 1.15.1"

using System;
using System.Linq;
using System.Reflection;

var qdrantAssembly = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "Microsoft.SemanticKernel.Connectors.Qdrant");

if (qdrantAssembly == null)
{
    var asm = Assembly.LoadFrom("/home/misango/.nuget/packages/microsoft.semantickernel.connectors.qdrant/1.68.0-preview/lib/net8.0/Microsoft.SemanticKernel.Connectors.Qdrant.dll");
    qdrantAssembly = asm;
}

Console.WriteLine("=== Qdrant Connector Types ===");
var types = qdrantAssembly.GetExportedTypes();
foreach (var type in types.OrderBy(t => t.Name))
{
    Console.WriteLine($"\n{type.FullName}");
    
    if (type.Name.Contains("Memory") || type.Name.Contains("Store") || type.Name.Contains("Qdrant"))
    {
        Console.WriteLine("  Constructors:");
        foreach (var ctor in type.GetConstructors())
        {
            var parameters = string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"    {type.Name}({parameters})");
        }
        
        Console.WriteLine("  Public Methods:");
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Take(10))
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Console.WriteLine($"    {method.ReturnType.Name} {method.Name}({parameters})");
        }
    }
}
