var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ProjectName_Api>("projectname-api");

builder.AddProject<Projects.ProjectName_GrpcService>("projectname-grpcservice");

builder.AddProject<Projects.ProjectName_Gateway>("projectname-gateway");

builder.AddProject<Projects.ProjectName_Worker>("projectname-worker");

builder.Build().Run();
