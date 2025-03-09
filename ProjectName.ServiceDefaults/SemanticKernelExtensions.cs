using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectName.ServiceDefaults
{
    public static class SemanticKernelExtensions
    {

        public static IServiceCollection AIService(this IServiceCollection services)
        {
            // Configure kernel with model tiering based on task complexity
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var kernelBuilder = Kernel.CreateBuilder();

                // Simple tasks - use faster, less expensive models
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: config["AI:FastModel"] ?? "gpt-3.5-turbo",
                    endpoint: config["AI:Endpoint"],
                    apiKey: config["AI:ApiKey"],
                    serviceId: "fast-completion");

                // Complex reasoning tasks - use more capable models
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: config["AI:AdvancedModel"] ?? "gpt-4-turbo",
                    endpoint: config["AI:Endpoint"],
                    apiKey: config["AI:ApiKey"],
                    serviceId: "advanced-completion");

                return kernelBuilder.Build();
            });
            return services;
        }
    }
}
