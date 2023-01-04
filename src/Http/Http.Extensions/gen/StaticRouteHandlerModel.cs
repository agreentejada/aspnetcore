// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http.SourceGeneration;

using Microsoft.CodeAnalysis;

public abstract class Component { }

public enum HttpMethod
{
    Get,
    Post,
    Put,
    Delete
}

public class Route : Component
{
    public string RoutePattern { get; set; }
    public List<string> RouteParameters { get; set; }
}

public class RequestParameter
{
    public string Name { get; set; }
    public string Type { get; set; }
}

public class EndpointResponse
{
    public string ResponseType { get; set; }
    public string ContentType { get; set; }
}

public class Endpoint : Component
{
    public HttpMethod HttpMethod { get; set; }
    public Route Route { get; set; }
    public List<RequestParameter> RequestParameters { get; set; }
    public EndpointResponse Response { get; set; }
    public IMethodSymbol MethodSymbol { get; set; }
}
