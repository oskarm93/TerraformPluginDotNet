﻿using Microsoft.Extensions.DependencyInjection;

namespace TerraformPluginDotNet.ResourceProvider;

class ServiceCollectionResourceRegistryContext : IResourceRegistryContext
{
    private readonly IServiceCollection _services;

    public ServiceCollectionResourceRegistryContext(IServiceCollection services)
    {
        _services = services;
    }

    public void RegisterResource<T>(string resourceName)
    {
        // Validation
        if (!typeof(T).IsPublic)
        {
            // Must be public to allow messagepack serialization.
            throw new InvalidOperationException($"Type {typeof(T).FullName} must be public in order to be used as a Terraform resource.");
        }

        _services.AddSingleton(new ResourceRegistryRegistration(resourceName, typeof(T)));
    }
}
