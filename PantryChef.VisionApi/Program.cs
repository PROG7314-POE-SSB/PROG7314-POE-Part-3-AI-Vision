using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Main entry point for the Azure Function application.
/// This file configures and runs the function host, setting up
/// dependency injection for services like the ImageAnalysisClient.
/// </summary>
var host = new HostBuilder()
   .ConfigureFunctionsWebApplication()
   .ConfigureServices(services =>
   {
       // Register the ImageAnalysisClient as a singleton.
       // This ensures that a single client instance is created and reused
       // for the lifetime of the function app, which is best practice.
       services.AddSingleton(sp =>
       {
           // Read connection details from environment variables
           var endpoint = Environment.GetEnvironmentVariable("VISION_ENDPOINT");
           var key = Environment.GetEnvironmentVariable("VISION_KEY");

           // Fail fast on startup if configuration is missing
           if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
           {
               throw new InvalidOperationException(
                   "VISION_ENDPOINT or VISION_KEY environment variables are not set. " +
                   "Please configure them in your local.settings.json or application settings."
               );
           }

           // Create and return the configured client
           return new ImageAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));
       });

       // Other services can be registered here
   })
   .Build();

// Start the function host
host.Run();