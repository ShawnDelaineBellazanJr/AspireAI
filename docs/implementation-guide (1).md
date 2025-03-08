# .NET Aspire AI System Implementation Guide

## Overview

This guide provides detailed implementation instructions for building a modular, scalable .NET Aspire AI-driven system using a hybrid communication approach with gRPC and REST APIs. The solution integrates Microsoft Semantic Kernel, Semantic-Kernel Prompty, and the Semantic Kernel Agent Framework, following Clean Architecture principles.

## Solution Structure

The solution follows Clean Architecture principles with the following projects:

```
AspireAI.sln
├── AspireAI.AppHost                  # .NET Aspire application host
├── AspireAI.ServiceDefaults          # Shared service configurations
├── AspireAI.Core                     # Domain models, interfaces, and contracts
├── AspireAI.Infrastructure           # Infrastructure implementations
├── AspireAI.AI                       # AI core components and services
├── AspireAI.Api                      # REST API service
├── AspireAI.GrpcService              # gRPC service
├── AspireAI.Gateway                  # API Gateway
├── AspireAI.Plugins                  # Plugin system
├── AspireAI.Worker                   # Background worker service
├── AspireAI.UnitTests                # Unit tests
└── AspireAI.IntegrationTests         # Integration tests
```

## Setting Up the Project Template

### Step 1: Creating the Project Template

1. Create a new .NET template pack:

```bash
dotnet new templatepack -o AspireAI.Template
cd AspireAI.Template
```

2. Create the template configuration file:

**AspireAI.Template.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageType>Template</PackageType>
    <PackageVersion>1.0.0</PackageVersion>
    <PackageId>AspireAI.Template</PackageId>
    <Title>AspireAI Template</Title>
    <Authors>Your Name</Authors>
    <Description>A .NET Aspire AI-driven system template</Description>
    <PackageTags>dotnet-new;templates;aspire;ai;semantic-kernel</PackageTags>
    <TargetFramework>net8.0</TargetFramework>
    <IncludeContentInPack>true</IncludeContentInPack>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <ContentTargetFolders>content</ContentTargetFolders>
    <NoWarn>$(NoWarn);NU5128</NoWarn>
    <NoDefaultExcludes>true</NoDefaultExcludes>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="content/**/*" Exclude="content/**/bin/**;content/**/obj/**" />
    <Compile Remove="**/*" />
  </ItemGroup>
</Project>
```

3. Add the template.json configuration file:

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Your Name",
  "classifications": ["Web", "ASP.NET Core", "Cloud", "AI", "Aspire"],
  "identity": "AspireAI.Template",
  "name": "Aspire AI System",
  "shortName": "aspire-ai",
  "tags": {
    "language": "C#",
    "type": "solution"
  },
  "sourceName": "AspireAI",
  "preferNameDirectory": true
}
```

### AspireAI.Gateway Project (API Gateway)

This project implements the API Gateway that routes and manages requests to the appropriate services.

**Key Files:**

```
AspireAI.Gateway/
├── Configuration/
│   └── RouteConfig.cs
├── Auth/
│   └── AuthenticationHandler.cs
└── Program.cs
```

**Program.cs**:
```csharp
using AspireAI.Gateway.Auth;
using Yarp.ReverseProxy.Transforms;
using Aspire.Hosting.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.AddServiceDefaults();

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transforms =>
    {
        // Add request transforms
        transforms.AddRequestTransform(transform =>
        {
            transform.ProxyRequest.Headers.Add("X-Forwarded-Host", transform.HttpContext.Request.Host.Value);
            return ValueTask.CompletedTask;
        });
    });

// Add authentication
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>("ApiKey", options => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiKeyPolicy", policy =>
        policy.RequireAuthenticatedUser());
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Observability
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();
```

**appsettings.json**:
```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": {
          "Path": "/api/{**catch-all}"
        },
        "Transforms": [
          {
            "PathPattern": "api/{**catch-all}"
          }
        ]
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "api": {
            "Address": "https://localhost:5001"
          }
        },
        "LoadBalancingPolicy": "RoundRobin"
      }
    }
  },
  "ApiKeys": {
    "ValidApiKeys": [
      "test-api-key-1",
      "test-api-key-2"
    ]
  }
}
```

**ApiKeyAuthHandler.cs**:
```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace AspireAI.Gateway.Auth
{
    public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private readonly IConfiguration _configuration;

        public ApiKeyAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IConfiguration configuration)
            : base(options, logger, encoder, clock)
        {
            _configuration = configuration;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Get API key from header
            if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeaderValues))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

            if (string.IsNullOrEmpty(providedApiKey))
            {
                return Task.

### AspireAI.Api Project (REST API)

This project implements the REST API service that exposes the AI capabilities to external clients.

**Key Files:**

```
AspireAI.Api/
├── Controllers/
│   ├── AIController.cs
│   └── PluginsController.cs
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   └── RequestLoggingMiddleware.cs
└── Program.cs
```

**AIController.cs**:
```csharp
using AspireAI.Core.Interfaces;
using AspireAI.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AspireAI.Api.Controllers
{
    [ApiController]
    [Route("api/v1/ai")]
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;
        private readonly ILogger<AIController> _logger;

        public AIController(IAIService aiService, ILogger<AIController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        [HttpPost("completion")]
        public async Task<ActionResult<AIResponse>> GetCompletion([FromBody] AIRequest request)
        {
            _logger.LogInformation("Received completion request");
            
            if (string.IsNullOrEmpty(request.Prompt))
            {
                return BadRequest("Prompt is required");
            }

            var response = await _aiService.ProcessRequestAsync(request);
            
            if (!response.IsSuccess)
            {
                return StatusCode(500, response.Error);
            }

            return Ok(response);
        }

        [HttpPost("stream-completion")]
        public async Task GetStreamingCompletion([FromBody] AIRequest request)
        {
            _logger.LogInformation("Received streaming completion request");
            
            if (string.IsNullOrEmpty(request.Prompt))
            {
                Response.StatusCode = 400;
                await Response.WriteAsync("Prompt is required");
                return;
            }

            Response.Headers.Add("Content-Type", "text/event-stream");
            
            // Create cancellation token linked to client disconnection
            var cancellationToken = HttpContext.RequestAborted;
            
            try
            {
                request.IsStreaming = true;
                var responseStream = await _aiService.GetStreamingCompletionAsync(
                    request.Prompt, 
                    request.Parameters, 
                    cancellationToken);
                
                await Response.WriteAsync(responseStream.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming completion");
                await Response.WriteAsync($"Error: {ex.Message}");
            }
        }
    }
}
```

**Program.cs**:
```csharp
using AspireAI.AI.Extensions;
using AspireAI.Api.Middleware;
using Aspire.Hosting.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add AI services
builder.Services.AddAIServices(builder.Configuration);

// Observability
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Add custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
```

### AspireAI.GrpcService Project (gRPC Service)

This project implements the gRPC service for high-performance internal communication.

**Key Files:**

```
AspireAI.GrpcService/
├── Protos/
│   ├── ai_service.proto
│   └── agent_service.proto
├── Services/
│   ├── AIGrpcService.cs
│   └── AgentGrpcService.cs
├── Interceptors/
│   └── LoggingInterceptor.cs
└── Program.cs
```

**ai_service.proto**:
```protobuf
syntax = "proto3";

option csharp_namespace = "AspireAI.GrpcService.Protos";

package ai;

service AIService {
  rpc GetCompletion (AIRequest) returns (AIResponse);
  rpc GetStreamingCompletion (AIRequest) returns (stream AIStreamingResponse);
}

message AIRequest {
  string prompt = 1;
  string model_id = 2;
  map<string, string> parameters = 3;
  int32 max_tokens = 4;
  float temperature = 5;
}

message AIResponse {
  string content = 1;
  bool is_success = 2;
  string error = 3;
  map<string, string> metadata = 4;
  int32 tokens_used = 5;
}

message AIStreamingResponse {
  string content_chunk = 1;
  bool is_final = 2;
}
```

**AIGrpcService.cs**:
```csharp
using AspireAI.Core.Interfaces;
using AspireAI.Core.Models;
using AspireAI.GrpcService.Protos;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AspireAI.GrpcService.Services
{
    public class AIGrpcService : Protos.AIService.AIServiceBase
    {
        private readonly IAIService _aiService;
        private readonly ILogger<AIGrpcService> _logger;

        public AIGrpcService(IAIService aiService, ILogger<AIGrpcService> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        public override async Task<Protos.AIResponse> GetCompletion(Protos.AIRequest request, ServerCallContext context)
        {
            _logger.LogInformation("gRPC GetCompletion called");
            
            var aiRequest = new Core.Models.AIRequest
            {
                Prompt = request.Prompt,
                ModelId = request.ModelId,
                Parameters = request.Parameters.ToDictionary(p => p.Key, p => p.Value),
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature
            };

            var response = await _aiService.ProcessRequestAsync(aiRequest);

            return new Protos.AIResponse
            {
                Content = response.Content,
                IsSuccess = response.IsSuccess,
                Error = response.Error,
                TokensUsed = response.TokensUsed
            };
        }

        public override async Task GetStreamingCompletion(Protos.AIRequest request, IServerStreamWriter<Protos.AIStreamingResponse> responseStream, ServerCallContext context)
        {
            _logger.LogInformation("gRPC GetStreamingCompletion called");
            
            var aiRequest = new Core.Models.AIRequest
            {
                Prompt = request.Prompt,
                ModelId = request.ModelId,
                Parameters = request.Parameters.ToDictionary(p => p.Key, p => p.Value),
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                IsStreaming = true
            };

            try
            {
                // This is a simplified example - in a real implementation you'd stream chunks
                var response = await _aiService.GetStreamingCompletionAsync(
                    aiRequest.Prompt, 
                    aiRequest.Parameters, 
                    context.CancellationToken);
                
                // In a real implementation, you would stream chunks as they come
                // This is a simplified version
                await responseStream.WriteAsync(new Protos.AIStreamingResponse
                {
                    ContentChunk = response.Content,
                    IsFinal = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in streaming completion");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }
    }
}
```

**Program.cs**:
```csharp
using AspireAI.AI.Extensions;
using AspireAI.GrpcService.Interceptors;
using AspireAI.GrpcService.Services;
using Aspire.Hosting.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.AddServiceDefaults();
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<LoggingInterceptor>();
});

// Add AI services
builder.Services.AddAIServices(builder.Configuration);

// Observability
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddGrpcClientInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapGrpcService<AIGrpcService>();
app.MapGrpcService<AgentGrpcService>();
app.MapDefaultEndpoints();

app.Run();
```

### AspireAI.AI Project

This project implements the AI services using Microsoft Semantic Kernel, Semantic-Kernel Prompty, and the Semantic Kernel Agent Framework.

**Key Files:**

```
AspireAI.AI/
├── Services/
│   ├── SemanticKernelService.cs
│   ├── PromptService.cs
│   └── AgentService.cs
├── Agents/
│   ├── BaseAgent.cs
│   ├── TextProcessingAgent.cs
│   └── ResearchAgent.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
└── Prompts/
    ├── text-completion.prompt
    ├── summarization.prompt
    └── question-answering.prompt
```

**SemanticKernelService.cs**:
```csharp
using AspireAI.Core.Interfaces;
using AspireAI.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AspireAI.AI.Services
{
    public class SemanticKernelService : IAIService
    {
        private readonly Kernel _kernel;
        private readonly IPromptService _promptService;
        private readonly ILogger<SemanticKernelService> _logger;

        public SemanticKernelService(Kernel kernel, IPromptService promptService, ILogger<SemanticKernelService> logger)
        {
            _kernel = kernel;
            _promptService = promptService;
            _logger = logger;
        }

        public async Task<AIResponse> ProcessRequestAsync(AIRequest request)
        {
            try
            {
                if (request.IsStreaming)
                {
                    return await GetStreamingCompletionAsync(request.Prompt, request.Parameters, CancellationToken.None);
                }
                else
                {
                    return await GetCompletionAsync(request.Prompt, request.Parameters);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI request");
                return new AIResponse
                {
                    IsSuccess = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<AIResponse> GetCompletionAsync(string prompt, IDictionary<string, string> parameters)
        {
            try
            {
                // Create kernel arguments from parameters
                var kernelArguments = new KernelArguments();
                foreach (var param in parameters)
                {
                    kernelArguments[param.Key] = param.Value;
                }

                // Execute the prompt
                var result = await _kernel.InvokePromptAsync(prompt, kernelArguments);
                
                return new AIResponse
                {
                    Content = result.ToString(),
                    IsSuccess = true,
                    TokensUsed = result.Metadata.TryGetValue("Usage.TotalTokens", out var tokensObj) 
                        ? Convert.ToInt32(tokensObj) 
                        : 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting completion");
                return new AIResponse
                {
                    IsSuccess = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<AIResponse> GetStreamingCompletionAsync(string prompt, IDictionary<string, string> parameters, CancellationToken cancellationToken)
        {
            try
            {
                // Create kernel arguments from parameters
                var kernelArguments = new KernelArguments();
                foreach (var param in parameters)
                {
                    kernelArguments[param.Key] = param.Value;
                }

                // Execute the prompt with streaming
                var result = await _kernel.InvokePromptStreamingAsync(prompt, kernelArguments);
                
                var contentBuilder = new StringBuilder();
                await foreach (var chunk in result)
                {
                    contentBuilder.Append(chunk);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                
                return new AIResponse
                {
                    Content = contentBuilder.ToString(),
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting streaming completion");
                return new AIResponse
                {
                    IsSuccess = false,
                    Error = ex.Message
                };
            }
        }

        public async Task<byte[]> GenerateImageAsync(string prompt, int width, int height)
        {
            // Implement image generation
            throw new NotImplementedException("Image generation not implemented yet");
        }
    }
}
```

**PromptService.cs**:
```csharp
using AspireAI.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace AspireAI.AI.Services
{
    public class PromptService : IPromptService
    {
        private readonly string _promptsDirectory;
        private readonly ILogger<PromptService> _logger;

        public PromptService(string promptsDirectory, ILogger<PromptService> logger)
        {
            _promptsDirectory = promptsDirectory;
            _logger = logger;
        }

        public async Task<string> GetPromptTemplateAsync(string templateId)
        {
            var filePath = Path.Combine(_promptsDirectory, $"{templateId}.prompt");
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Prompt template not found: {TemplateId}", templateId);
                return string.Empty;
            }

            return await File.ReadAllTextAsync(filePath);
        }

        public async Task<string> RenderPromptAsync(string templateId, IDictionary<string, string> parameters)
        {
            var template = await GetPromptTemplateAsync(templateId);
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            var renderedPrompt = template;
            foreach (var param in parameters)
            {
                renderedPrompt = renderedPrompt.Replace($"{{{param.Key}}}", param.Value);
            }

            return renderedPrompt;
        }

        public async Task SavePromptTemplateAsync(string templateId, string promptTemplate)
        {
            var filePath = Path.Combine(_promptsDirectory, $"{templateId}.prompt");
            Directory.CreateDirectory(_promptsDirectory);
            await File.WriteAllTextAsync(filePath, promptTemplate);
        }

        public async Task<IEnumerable<string>> GetAvailableTemplatesAsync()
        {
            if (!Directory.Exists(_promptsDirectory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.GetFiles(_promptsDirectory, "*.prompt")
                .Select(Path.GetFileNameWithoutExtension);
        }
    }
}
```

**ServiceCollectionExtensions.cs**:
```csharp
using AspireAI.Core.Interfaces;
using AspireAI.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System;

namespace AspireAI.AI.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAIServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure and register Semantic Kernel
            services.AddSingleton(sp =>
            {
                var builder = Kernel.CreateBuilder();
                
                // Configure OpenAI
                var openAIApiKey = configuration["AI:OpenAI:ApiKey"];
                var openAIModelId = configuration["AI:OpenAI:ModelId"];
                
                if (!string.IsNullOrEmpty(openAIApiKey))
                {
                    builder.AddOpenAIChatCompletion(openAIModelId, openAIApiKey);
                }
                
                // Configure Azure OpenAI if specified
                var azureOpenAIEndpoint = configuration["AI:AzureOpenAI:Endpoint"];
                var azureOpenAIApiKey = configuration["AI:AzureOpenAI:ApiKey"];
                var azureOpenAIDeploymentName = configuration["AI:AzureOpenAI:DeploymentName"];
                
                if (!string.IsNullOrEmpty(azureOpenAIEndpoint) && !string.IsNullOrEmpty(azureOpenAIApiKey))
                {
                    builder.AddAzureOpenAIChatCompletion(
                        azureOpenAIDeploymentName,
                        azureOpenAIEndpoint,
                        azureOpenAIApiKey);
                }
                
                // Add memory if Redis is configured
                var redisConnectionString = configuration["AI:Memory:RedisConnectionString"];
                if (!string.IsNullOrEmpty(redisConnectionString))
                {
                    // Configure Redis memory store
                    builder.Services.AddSingleton(sp => 
                    {
                        // Add Redis memory configuration here
                    });
                }
                
                return builder.Build();
            });
            
            // Configure prompt service
            services.AddSingleton<IPromptService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<PromptService>>();
                var promptsDirectory = configuration["AI:PromptsDirectory"] ?? "Prompts";
                return new PromptService(promptsDirectory, logger);
            });
            
            // Register the AI service
            services.AddSingleton<IAIService, SemanticKernelService>();
            
            return services;
        }
    }
}
```

4. Install the template:

```bash
dotnet pack
dotnet new install bin/Debug/AspireAI.Template.1.0.0.nupkg
```

5. Create a new project using the template:

```bash
dotnet new aspire-ai -n MyAspireAI
cd MyAspireAI
```

### Step 2: Core Projects Implementation

Let's now implement the core components of our .NET Aspire AI system.

## Project Implementation

### AspireAI.Core Project

This project contains domain models, interfaces, and contracts that define the core business logic.

**Key Files:**

```
AspireAI.Core/
├── Models/
│   ├── AIRequest.cs
│   ├── AIResponse.cs
│   └── AgentTask.cs
├── Interfaces/
│   ├── IAIService.cs
│   ├── IPromptService.cs
│   └── IPluginRegistry.cs
└── Constants/
    └── AIConstants.cs
```

**IAIService.cs**:
```csharp
using AspireAI.Core.Models;
using System.Threading.Tasks;

namespace AspireAI.Core.Interfaces
{
    public interface IAIService
    {
        Task<AIResponse> ProcessRequestAsync(AIRequest request);
        Task<AIResponse> GetCompletionAsync(string prompt, IDictionary<string, string> parameters);
        Task<AIResponse> GetStreamingCompletionAsync(string prompt, IDictionary<string, string> parameters, CancellationToken cancellationToken);
        Task<byte[]> GenerateImageAsync(string prompt, int width, int height);
    }
}
```

**IPromptService.cs**:
```csharp
namespace AspireAI.Core.Interfaces
{
    public interface IPromptService
    {
        Task<string> GetPromptTemplateAsync(string templateId);
        Task<string> RenderPromptAsync(string templateId, IDictionary<string, string> parameters);
        Task SavePromptTemplateAsync(string templateId, string promptTemplate);
        Task<IEnumerable<string>> GetAvailableTemplatesAsync();
    }
}
```

**AIRequest.cs**:
```csharp
namespace AspireAI.Core.Models
{
    public class AIRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new();
        public string ModelId { get; set; } = string.Empty;
        public bool IsStreaming { get; set; }
        public int MaxTokens { get; set; } = 1000;
        public float Temperature { get; set; } = 0.7f;
    }
}
```

**AIResponse.cs**:
```csharp
namespace AspireAI.Core.Models
{
    public class AIResponse
    {
        public string Content { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Error { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public int TokensUsed { get; set; }
    }
}
```