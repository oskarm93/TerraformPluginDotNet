using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using TerraformPluginDotNet.ResourceProvider;

namespace TerraformPluginDotNet;

/// <summary>
/// Use this class to create a default host for a Terraform plugin.
/// </summary>
public static class TerraformPluginHost
{
    public static async Task RunAsync(string[] args, string fullProviderName, Action<IServiceCollection, IResourceRegistryContext> configure, CancellationToken token = default)
    {
        var serilogConfiguration = new ConfigurationBuilder()
            .AddJsonFile("serilog.json", optional: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(serilogConfiguration)
            .CreateBootstrapLogger();

        try
        {
            await CreateHostBuilder(args, fullProviderName, configure).Build().RunAsync(token);
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, "Fatal error occurred.");
        }
        finally
        {
            Log.Logger.Information("Application terminated.");
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(
        string[] args,
        string fullProviderName,
        Action<IServiceCollection, IResourceRegistryContext> configure) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "serilog.json"), optional: true);
                configuration.AddJsonFile("serilog.json", optional: true);
            })
            .ConfigureServices((host, services) =>
            {
                services.Configure<TerraformPluginHostOptions>(host.Configuration);
                services.Configure<TerraformPluginHostOptions>(x => x.FullProviderName = fullProviderName);
                services.AddSingleton(new PluginHostCertificate
                {
                    Certificate = CertificateGenerator.GenerateSelfSignedCertificate("CN=127.0.0.1", "CN=root ca", CertificateGenerator.GeneratePrivateKey()),
                });
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureTerraformPlugin(configure);
            })
            .UseSerilog((context, services, configuration) =>
            {
                configuration
                      .ReadFrom.Configuration(context.Configuration)
                      .ReadFrom.Services(services)
                      .Enrich.FromLogContext();

                // Only write to console in debug mode because Terraform reads connection details from stdout.
                if (services.GetRequiredService<IOptions<TerraformPluginHostOptions>>().Value.DebugMode)
                {
                    configuration.WriteTo.Console();
                }
            });
}
