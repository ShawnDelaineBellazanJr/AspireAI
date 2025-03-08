# AspireAI - .NET Aspire AI-Driven System

A comprehensive, scalable, and modular AI-driven system built with .NET Aspire, integrating Semantic Kernel, Agent Framework, and hybrid API communication (gRPC + REST).

## 🌟 Features

- **Hybrid API Communication**: gRPC for internal high-performance communication, REST API for external clients
- **AI Integration**: Microsoft Semantic Kernel, SK Prompty, and Agent Framework integration
- **Modular Architecture**: Clean Architecture principles with clear separation of concerns
- **Dynamic Plugin System**: Extensible architecture for custom AI capabilities
- **Observability**: Full tracing, logging, and monitoring with OpenTelemetry
- **Scalable**: Built with .NET Aspire for distributed cloud-native applications
- **Secure**: Authentication, authorization, and rate limiting built-in

## 📋 System Components

- **API Gateway**: Routes external requests and provides authentication/rate limiting
- **REST API Service**: External-facing API with OpenAPI documentation
- **gRPC Service**: High-performance internal communication service
- **Plugin System**: Dynamic loading of AI capabilities
- **Worker Service**: Background processing for AI tasks
- **AI Core**: Semantic Kernel integration with prompt management
- **Redis Cache**: For performance and distributed state

## 🛠️ Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Docker and Docker Compose (for containerized deployment)
- Redis (for caching and distributed state)

### Quick Start

1. **Install the template**:
   ```bash
   dotnet new install AspireAI.Template
   ```

2. **Create a new project**:
   ```bash
   dotnet new aspire-ai -n MyAspireAIProject
   cd MyAspireAIProject
   ```

3. **Configure your AI providers**:
   Edit `appsettings.json` to add your API keys:
   ```json
   {
     "AI": {
       "OpenAI": {
         "ApiKey": "your-api-key",
         "ModelId": "gpt-4"
       }
     }
   }
   ```

4. **Run the project**:
   ```bash
   dotnet run --project MyAspireAIProject.AppHost
   ```

5. **Access the services**:
   - API Gateway: https://localhost:8443
   - Swagger UI: https://localhost:8443/swagger
   - Grafana Dashboard: http://localhost:3000

### Docker Deployment

Use Docker Compose to run the complete system:

```bash
# Set OpenAI API key
export OPENAI_API_KEY=your-api-key

# Run the system
docker-compose up -d
```

## 🧩 Solution Structure

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

## 🧠 AI Integration

### Semantic Kernel

The system integrates with Microsoft Semantic Kernel for AI orchestration:

```csharp
// Configure Semantic Kernel
services.AddSingleton(sp =>
{
    var builder = Kernel.CreateBuilder();
    
    // Add OpenAI integration
    builder.AddOpenAIChatCompletion(
        modelId: "gpt-4",
        apiKey: Configuration["AI:OpenAI:ApiKey"]);
    
    return builder.Build();
});
```

### Agent Framework

Multi-agent workflows are supported through the Semantic Kernel Agent Framework:

```csharp
// Agent system setup
services.AddSingleton<IAgentService>(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    var logger = sp.GetRequiredService<ILogger<AgentService>>();
    
    return new AgentService(kernel, logger);
});
```

### Prompt Management

Prompts are managed through the Semantic Kernel Prompty integration:

```csharp
// Register prompt service
services.AddSingleton<IPromptService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PromptService>>();
    var promptsDirectory = Configuration["AI:PromptsDirectory"] ?? "Prompts";
    return new PromptService(promptsDirectory, logger);
});
```

## 🔌 Plugin System

Create custom plugins by implementing the `IPlugin` interface:

```csharp
public class MyCustomPlugin : IPlugin
{
    public string Name => "MyCustomPlugin";
    public string Version => "1.0.0";
    public string Description => "A custom plugin for AspireAI";
    
    public Task InitializeAsync()
    {
        // Initialize plugin resources
        return Task.CompletedTask;
    }
    
    public async Task<object> ExecuteAsync(string operation, IDictionary<string, object> parameters)
    {
        // Implement plugin functionality
        return "Plugin executed successfully";
    }
}
```

## 🔍 Observability

The system includes comprehensive observability with OpenTelemetry:

- **Tracing**: Distributed tracing across all services
- **Metrics**: Custom and standard metrics in Prometheus
- **Logging**: Structured logging with correlation IDs
- **Dashboards**: Pre-configured Grafana dashboards

## 🔒 Security

Security features include:

- **API Key Authentication**: For external API access
- **Rate Limiting**: To prevent abuse
- **CORS Configuration**: For web clients
- **HTTPS Enforcement**: For all external communication
- **Secret Management**: For API keys and sensitive configuration

## 📚 API Reference

The API is documented with OpenAPI/Swagger and available at `/swagger` endpoint.

Main endpoints:

- `POST /api/v1/ai/completion`: Get an AI completion
- `POST /api/v1/ai/stream-completion`: Get a streaming AI completion
- `POST /api/v1/ai/template/{templateId}`: Use a specific prompt template

## 🧪 Testing

Run the included tests:

```bash
dotnet test
```

Test coverage includes:

- **Unit Tests**: Testing individual components
- **Integration Tests**: Testing service interactions
- **Load Tests**: Testing system under load (with JMeter)

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request