using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;

using SupportHub.OrganizationAccesss.Infrastructure.Data;
using SupportHub.OrganizationAccesss.Infrastructure.Messaging;

using SupportHub.OrganizationAccesss.Features.Users.GetUsers;
using SupportHub.OrganizationAccesss.Features.Users.CreateUser;
using SupportHub.OrganizationAccesss.Features.Users.GetUser;
using SupportHub.OrganizationAccesss.Features.Users.DeleteUser;
using SupportHub.OrganizationAccesss.Features.Users.UpdateUser;
using SupportHub.OrganizationAccesss.Features.Organizations.CreateOrganization;
using SupportHub.OrganizationAccesss.Features.Organizations.GetOrganizations;
using SupportHub.OrganizationAccesss.Features.Organizations.GetOrganization;
using SupportHub.OrganizationAccesss.Features.Organizations.DeleteOrganization;
using SupportHub.OrganizationAccesss.Features.Organizations.UpdateOrganization;
using SupportHub.OrganizationAccesss.Features.Organizations.CreationConfirmation;
using SupportHub.OrganizationAccesss.Infrastructure.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using SupportHub.OrganizationAccesss.Features.Users.InvitationConfirmation;

var builder = WebApplication.CreateBuilder(args);

//Add a Serilog logger with console output and configuration from appsettings.json
builder.Host.UseSerilog((ctx, config) =>
{
    config.ReadFrom.Configuration(ctx.Configuration)
          .Filter.ByExcluding("RequestPath like '%/health%'");
});

//Allow enums to be serialized as strings in JSON responses for better readability in API responses and documentation
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: true));
});

//Add support for discovery and documenting API endpoints in the application
builder.Services.AddEndpointsApiExplorer();

//Add Swagger/OpenAPI support for API documentation and testing
builder.Services.AddSwaggerGen();

//Enable health monitoring of the application
builder.Services.AddHealthChecks();

//Add CORS policy
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

//Add MediatR for in-process messaging and CQRS pattern implementation
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

//Add database context
builder.AddSqlServerDbContext<OrganizationDbContext>("OrgDb");

//Bind KafkaSettings from appsettings.json and override with Aspire-injected connection string if available
builder.Services.Configure<KafkaSettings>(options =>
{
    //Bind the standard settings from appsettings.json
    builder.Configuration.GetSection("Kafka").Bind(options);

    //Fetch the Aspire-injected connection string for Kafka
    var aspireKafkaConnectionString = builder.Configuration.GetConnectionString("kafka");

    //If Aspire provided a connection string, use it instead of localhost
    if (!string.IsNullOrEmpty(aspireKafkaConnectionString))
    {
        options.BootstrapServers = aspireKafkaConnectionString;
    }
});

//Register the Publisher for feature handlers to inject
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

//Register the Background Kafka Consumer
builder.Services.AddHostedService<OrganizationEventConsumerService>();

//Fetch authentication settings from configuration
var authority = builder.Configuration["Authentication:Authority"]!;
var audience = builder.Configuration["Authentication:Audience"]!;

//Validate that required authentication settings are present
if (string.IsNullOrWhiteSpace(authority))
{
    throw new InvalidOperationException(
        "Authentication:Authority is not configured. " +
        "Please set it in appsettings.json (e.g., 'https://keycloak.example.com/realms/support-hub')");
}
if (string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException(
        "Authentication:Audience is not configured. " +
        "Please set it in appsettings.json (e.g., 'ticket-management-service')");
}
//Validate that Authority is a valid URL
if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) || authorityUri.Scheme != "https" && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        $"Authentication:Authority must be a valid HTTPS URL. Received: '{authority}'. " +
        "HTTP is only allowed in Development environment.");
}

//Add JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        //Keycloak uses asymmetric RSA keys published at the JWKS endpoint
        options.Authority = authority;
        options.Audience = audience;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "http://supporthub.local/realms/supporthub",
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        };
        //Keycloak-specific configuration for development environments
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = false
            };
        }
    });

//Add authorization services to enable role-based or policy-based access control for API endpoints
builder.Services.AddAuthorization();

//Add exception handling middleware
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

//Configure the application to look for and honor proxy network signatures
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    
    //Clear restrictions for containerized architectures where upstream proxy IPs change dynamically
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

//Handle forwarded headers for reverse proxy scenarios
app.UseForwardedHeaders();

//Add exception handling middleware
app.UseExceptionHandler(_ => {});

//Add HTTP request logging middleware to log incoming requests and outgoing responses for better observability and debugging
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        //If an exception happens log them at Error level
        if (ex != null) return Serilog.Events.LogEventLevel.Error;
        if (httpContext.Response.StatusCode >= 500) return Serilog.Events.LogEventLevel.Error;

        //Mute periodic Kubernetes health probes by demoting them to verbose
        var path = httpContext.Request.Path.Value;
        if (path != null && path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            return Serilog.Events.LogEventLevel.Verbose;
        }

        return Serilog.Events.LogEventLevel.Information;
    };
});

//Register swagger when in development environment for API documentation and testing
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//Redirect HTTP requests to HTTPS
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

//Apply the defined CORS policy to allow cross-origin requests from the frontend application and other clients
app.UseCors("DefaultCorsPolicy");

//Enable authentication and authorization middleware to protect API endpoints and ensure only authorized users can access them
app.UseAuthentication();
app.UseAuthorization();

//Register health check endpoints
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapMetrics();

//Map User API endpoints
app.MapUpdateUserEndpoint();
app.MapCreateUserEndpoint();
app.MapDeleteUserEndpoint();
app.MapGetUserEndpoint();
app.MapGetUsersEndpoint();
app.MapInvitationConfirmationEndpoint();

//Map Organization API endpoints
app.MapCreateOrganizationEndpoint();
app.MapGetOrganizationsEndpoint();
app.MapGetOrganizationEndpoint();
app.MapDeleteOrganizationEndpoint();
app.MapUpdateOrganizationEndpoint();
app.MapCreationConfirmationEndpoint();

//Apply any pending database migrations on application startup to ensure the database schema is up to date with the application's data model
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        await dbContext.Database.EnsureCreatedAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "An error occurred while migrating the database.");
        throw;
    }
}

//Start the application
try
{
    await app.SeedGlobalIdentitiesAsync();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start correctly!");
}
finally
{
    Log.CloseAndFlush();
}
