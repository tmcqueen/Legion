using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Legion.Admin.Data;

public static partial class Extensions
{
    public static IServiceCollection AddAuthenticationServices(this IHostApplicationBuilder builder)
    {
        var authority = builder.Configuration["OpenIddict:Authority"];
        ArgumentException.ThrowIfNullOrEmpty(authority, "OpenIddict:Authority");
        var clientId = builder.Configuration["OpenIddict:BffClientId"];
        ArgumentException.ThrowIfNullOrEmpty(clientId, "OpenIddict:BffClientId");
        var clientSecret = builder.Configuration["OpenIddict:BffClientSecret"];
        ArgumentException.ThrowIfNullOrEmpty(clientSecret, "OpenIddict:BffClientSecret");
        var isDevelopment = builder.Environment.IsDevelopment();
        var bffClientScopes = builder.Configuration.GetSection("OpenIddict:BffClientScopes").Get<string[]>();
        ArgumentNullException.ThrowIfNull(bffClientScopes, "OpenIddict:BffClientScopes");


        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
        })
        
        .AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.LoginPath = "/account/login";
            options.LogoutPath = "/account/logout";
        })

        .AddOpenIdConnect(options =>
        {
            options.Authority = authority;
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.Scope.Clear();
            options.Scope.ToList().AddRange(bffClientScopes);
            if (isDevelopment)
            {
                options.RequireHttpsMetadata = false;
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            }
        });
        return builder.Services;
    }
}