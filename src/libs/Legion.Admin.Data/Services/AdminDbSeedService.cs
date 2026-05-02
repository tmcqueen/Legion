
using Legion.Admin.Data.Seeds;
using Legion.Admin.Data.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Legion.Admin.Data.Models.Auth;

namespace Legion.Admin.Data.Services;

public class AdminDbSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _env;
    private readonly ILogger<AdminDbSeedService> _logger;
    private readonly string _authority;

    public AdminDbSeedService(ILogger<AdminDbSeedService> logger,
                              IServiceProvider serviceProvider,
                              IWebHostEnvironment env,
                             IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _env = env.EnvironmentName;
        _logger = logger;
        _authority = configuration["OpenIddict:Authority"]           
            ?? throw new InvalidOperationException("OpenIddict:Authority is required in configuration.")   ;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}


