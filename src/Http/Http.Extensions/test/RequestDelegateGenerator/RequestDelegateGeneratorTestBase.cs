// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.AspNetCore.Http.SourceGeneration;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace Microsoft.AspNetCore.Http.SourceGeneration.Tests;

public class RequestDelegateGeneratorTestBase
{
    internal static (ImmutableArray<GeneratorRunResult>, Compilation) RunGenerator(string sources)
    {
        var compilation = CreateCompilation(sources);
        var generator = new RequestDelegateGenerator().AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators: new[] { generator },
                                                              driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        // Run the source generator
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation,
                                                          out var outputDiagnostics);
        var diagnostics = updatedCompilation.GetDiagnostics();
        Assert.Empty(diagnostics.Where(d => d.Severity > DiagnosticSeverity.Warning));
        var runResult = driver.GetRunResult();

        return (runResult.Results, compilation);
    }

    private static Compilation CreateCompilation(string sources)
    {
        var source = $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class TestMapActions
{
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder app)
    {
        {{sources}}
        return app;
    }
}
""";

        var syntaxTrees = new[] { CSharpSyntaxTree.ParseText(source, path: $"TestMapActions.cs") };

        // Add in required metadata references
        var resolver = new AppLocalResolver();
        var references = new List<PortableExecutableReference>();
        var dependencyContext = DependencyContext.Load(typeof(RequestDelegateGeneratorTestBase).Assembly);

        Assert.NotNull(dependencyContext);

        foreach (var defaultCompileLibrary in dependencyContext.CompileLibraries)
        {
            foreach (var resolveReferencePath in defaultCompileLibrary.ResolveReferencePaths(resolver))
            {
                // Skip the source generator itself
                if (resolveReferencePath.Equals(typeof(RequestDelegateGenerator).Assembly.Location, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                references.Add(MetadataReference.CreateFromFile(resolveReferencePath));
            }
        }

        // Create a Roslyn compilation for the syntax tree.
        var compilation = CSharpCompilation.Create(assemblyName: Guid.NewGuid().ToString(),
                                                   syntaxTrees: syntaxTrees,
                                                   references: references,
                                                   options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation;
    }

    private sealed class AppLocalResolver : ICompilationAssemblyResolver
    {
        public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string> assemblies)
        {
            foreach (var assembly in library.Assemblies)
            {
                var dll = Path.Combine(Directory.GetCurrentDirectory(), "refs", Path.GetFileName(assembly));
                if (File.Exists(dll))
                {
                    assemblies ??= new();
                    assemblies.Add(dll);
                    return true;
                }

                dll = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(assembly));
                if (File.Exists(dll))
                {
                    assemblies ??= new();
                    assemblies.Add(dll);
                    return true;
                }
            }

            return false;
        }
    }
}
