// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.AspNetCore.Http.SourceGeneration;

[Generator]
public class RequestDelegateGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var mapActionOperations = context.SyntaxProvider.CreateSyntaxProvider(
                                                                    predicate: (node, _) => IsMapActionInvocation(node),
                                                                    transform: (context, _) =>
                                                                        context.SemanticModel.GetOperation(context.Node,
                                                                            _) as IInvocationOperation)
            .WithTrackingName("EndpointOperations");

        var endpoints = mapActionOperations.Select((operation, _) => GetEndpointFromOperation(operation)).WithTrackingName("EndpointModel");

        context.RegisterSourceOutput(endpoints, (context, source) => { });
    }

    private static readonly string[] KnownMethods = {"MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch", "Map"};

    private static bool IsMapActionInvocation(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name: IdentifierNameSyntax
                {
                    Identifier: {ValueText: var method}
                }
            },
            ArgumentList: {Arguments: {Count: 2} args}
        } mapActionCall && KnownMethods.Contains(method);
    }

    public static Endpoint GetEndpointFromOperation(IInvocationOperation operation)
    {
        var endpoint = new Endpoint();
        var routePatternArgument = operation.Arguments[1];
        var method = ResolveMethodFromOperation(operation.Arguments[2]);
        endpoint.Route = GetRouteFromArgument(routePatternArgument);
        endpoint.Response = GetResponseFromMethod(method);
        return endpoint;
    }

    public static Route GetRouteFromArgument(IArgumentOperation argumentOperation)
    {
        var syntax = argumentOperation.Syntax as ArgumentSyntax;
        var expression = syntax.Expression as LiteralExpressionSyntax;
        return new Route
        {
            RoutePattern = expression.Token.ValueText
        };
    }

    public static EndpointResponse GetResponseFromMethod(IMethodSymbol method)
    {
        var contentType = method.ReturnType.ToString() == "string" ? "plain/text" : "application/json";
        return new EndpointResponse
        {
            ContentType = contentType,
            ResponseType = method.ReturnType.ToString(),
        };
    }

    private static IMethodSymbol ResolveMethodFromOperation(IOperation operation) => operation switch
    {
        IArgumentOperation argument => ResolveMethodFromOperation(argument.Value),
        IConversionOperation conv => ResolveMethodFromOperation(conv.Operand),
        IDelegateCreationOperation del => ResolveMethodFromOperation(del.Target),
        IFieldReferenceOperation { Field.IsReadOnly: true } f when ResolveDeclarationOperation(f.Field, operation.SemanticModel) is IOperation op =>
            ResolveMethodFromOperation(op),
        IAnonymousFunctionOperation anon => anon.Symbol,
        ILocalFunctionOperation local => local.Symbol,
        IMethodReferenceOperation method => method.Method,
        _ => null
    };

    private static IOperation ResolveDeclarationOperation(ISymbol symbol, SemanticModel semanticModel)
    {
        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            var syn = syntaxReference.GetSyntax();

            if (syn is VariableDeclaratorSyntax
                {
                    Initializer:
                    {
                        Value: var expr
                    }
                })
            {
                // Use the correct semantic model based on the syntax tree
                var operation = semanticModel.GetOperation(expr);

                if (operation is not null)
                {
                    return operation;
                }
            }
        }

        return null;
    }
}
