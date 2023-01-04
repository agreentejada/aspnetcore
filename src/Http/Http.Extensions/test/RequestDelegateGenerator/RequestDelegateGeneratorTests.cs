// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http.SourceGeneration.Tests;

public class RequestDelegateGeneratorTests : RequestDelegateGeneratorTestBase
{
    [Fact]
    public void MapGet_StringQueryParam_StringReturn()
    {
        var source = """
app.MapGet("/hello/{name}", (string name) => $"Hello {name}!");
""";
        var results = RunGenerator(source);

        var endpoints = results.Item1[0].TrackedSteps["EndpointOperations"];
    }
}
