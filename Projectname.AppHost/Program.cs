var builder = DistributedApplication.CreateBuilder(args);



var api = builder.AddProject<Projects.ProjectName_Api>("api");

var grpc = builder.AddProject<Projects.ProjectName_GrpcService>("grpc");

builder.AddProject<Projects.ProjectName_Gateway>("gateway")
    .WithReference(api)
    .WithReference(grpc);

// Add Redis for distributed caching
//var redis = builder.AddRedis("redis");

builder.AddProject<Projects.ProjectName_Worker>("worker");

builder.Build().Run();
