using api_server.Auth;
using api_server.Controllers.Media;
using api_server.core;
using Asp.Versioning;
using exs.commons.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using mf.aiApi.mainDatabase.DatabaseContext;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var directoryName = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
// Load configs directory
if (Directory.Exists(directoryName + "/configs"))
{
    foreach (var jsonFilename in Directory.EnumerateFiles(directoryName + "/configs", "*.json", SearchOption.AllDirectories))
    {
        Console.WriteLine($"Loading /configs configuration: {jsonFilename}");
        builder.Configuration.AddJsonFile(jsonFilename, optional: false, reloadOnChange: false);
    }
}
if (builder.Environment.IsDevelopment())
{
    // Load all configuration files from local_configs first
    if (Directory.Exists(directoryName + "/local_configs"))
    {
        foreach (var jsonFilename in Directory.EnumerateFiles(directoryName + "/local_configs", "*.json", SearchOption.AllDirectories))
        {
            Console.WriteLine($"Loading dev /local_configs configuration: {jsonFilename}");
            builder.Configuration.AddJsonFile(jsonFilename, optional: false, reloadOnChange: false);
        }
    }
}
else
{
    // Load all configuration files from local_configs first
    if (Directory.Exists(directoryName + "/../local_configs"))
    {
        foreach (var jsonFilename in Directory.EnumerateFiles(directoryName + "/../local_configs", "*.json", SearchOption.AllDirectories))
        {
            Console.WriteLine($"Loading prod /local_configs configuration: {jsonFilename}");
            builder.Configuration.AddJsonFile(jsonFilename, optional: false, reloadOnChange: false);
        }
    }
}

try
{
    // Configure Serilog AFTER loading all configurations
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Application", "base-server-side API");
    });

    // Add services to the container.

    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        ConfigureJson.FillSerializerSettings(options.JsonSerializerOptions);
    });

    // AddOpenApi()'s schema generator reads Microsoft.AspNetCore.Http.Json.JsonOptions,
    // not the MVC JsonOptions configured above — without this, the generated OpenAPI
    // document (and anything code-generated from it, e.g. the SPA's typed client)
    // describes camelCase properties while the wire format above is actually PascalCase.
    builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
    {
        ConfigureJson.FillSerializerSettings(options.SerializerOptions);
    });


    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        var defaultFactory = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var errors = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value!.Errors.Select(e => e.ErrorMessage))}");
            logger.LogWarning("Invalid request params for {Path}: {Errors}", context.HttpContext.Request.Path, string.Join("; ", errors));
            return defaultFactory(context);
        };
    });

    DiHelper.RegisterServices(builder.Services, builder.Configuration);

    // RFC 7807 error responses everywhere; every problem carries the traceId so it
    // can be looked up in the telemetry backend.
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] =
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier;
    });
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // /health/live = process up; /health/ready = dependencies (DB) answer.
    builder.Services.AddHealthChecks()
        .AddCheck<DbReadyHealthCheck>("database", tags: ["ready"]);

    // Login brute-force protection: fixed window per client IP. Sized via config so
    // tests/load environments can raise it (RateLimiting:Login:PermitLimit).
    var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:Login:PermitLimit", 5);
    // refresh/checkSession are [AllowAnonymous] and write to the DB on every call —
    // unbounded like this they're an easy DB-write-amplification target even though the
    // rotation key itself isn't guessable. Higher limit than login: legitimate traffic
    // hits these far more often (every app start/expired token, not just sign-in).
    var refreshPermitLimit = builder.Configuration.GetValue("RateLimiting:Refresh:PermitLimit", 30);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
        options.AddPolicy("refresh", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = refreshPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    });

    // Unversioned calls map to v1, so current SPA routes keep working; future
    // breaking changes add [ApiVersion("2.0")] instead of new route trees.
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    });

    // Honors X-Forwarded-For/-Proto from a reverse proxy on the same host (the
    // default trusted proxy is loopback; add KnownProxies/KnownNetworks via code
    // here when the proxy lives elsewhere). Rate limiting and HTTPS detection
    // depend on this being correct.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    });

    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
    builder.Services.AddSingleton<TokenService>();

    var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
        ?? throw new InvalidOperationException("Missing required 'Jwt' configuration section.");
    if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
    {
        throw new InvalidOperationException("Missing required 'Jwt:SigningKey' configuration value.");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            // The access token lives in SPA memory and is sent as a normal
            // "Authorization: Bearer <token>" header — the default JwtBearer
            // token retrieval already handles that, so no cookie lookup here.
            options.Events = new JwtBearerEvents
            {
                // Refresh tokens are signed with the same key, so reject one here
                // if it's ever presented as an access token (e.g. mixup).
                OnTokenValidated = context =>
                {
                    if (context.Principal?.FindFirst("token_use")?.Value != "access")
                    {
                        context.Fail("Token is not an access token.");
                    }
                    return Task.CompletedTask;
                },
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        // Example role policy — see core/AuthPolicies.cs for usage.
        options.AddPolicy(AuthPolicies.Admin, policy => policy.RequireRole(AuthPolicies.AdminRole));
    });

    // OpenTelemetry: traces + metrics. Logs stay with Serilog (its output templates
    // carry {TraceId} so log lines join to traces). The OTLP exporter is registered
    // only when an endpoint is configured — via the standard OTEL_EXPORTER_OTLP_ENDPOINT
    // environment variable or an "OpenTelemetry:OtlpEndpoint" key in configs/ — so
    // machines without a telemetry backend pay nothing and log no export errors.
    var configOtlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
    var otlpEnabled = configOtlpEndpoint is not null
        || builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is not null;

    // Only the configs/-file key is applied explicitly; when the standard
    // OTEL_EXPORTER_OTLP_* environment variables are used, the SDK reads them itself
    // (endpoint, protocol, headers), so ops keep the full standard surface.
    Action<OpenTelemetry.Exporter.OtlpExporterOptions> configureOtlp = options =>
    {
        if (configOtlpEndpoint is not null)
        {
            options.Endpoint = new Uri(configOtlpEndpoint);
        }
    };

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: "base-server-side",
            serviceVersion: Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            serviceInstanceId: Environment.MachineName))
        .WithTracing(tracing =>
        {
            tracing
                .AddSource(Telemetry.SourceName) // custom spans (see core/Telemetry.cs)
                .AddAspNetCoreInstrumentation(options =>
                {
                    // The client-log intake receives large diagnostic payloads on a
                    // throttle; tracing it adds volume without diagnostic value.
                    options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/api/client-log");
                })
                .AddHttpClientInstrumentation()
                .AddNpgsql();
            if (otlpEnabled)
            {
                tracing.AddOtlpExporter(configureOtlp);
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddMeter(Telemetry.SourceName) // custom metrics (see core/Telemetry.cs)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();
            if (otlpEnabled)
            {
                metrics.AddOtlpExporter(configureOtlp);
            }
        });

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("SpaPolicy", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // required so the browser sends the auth cookie
        });
    });

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi(options =>
    {
        options.AddSchemaTransformer<OptionalPropertySchemaTransformer>();
        options.AddSchemaTransformer<UnixDateTimeSchemaTransformer>();
        options.AddSchemaTransformer<PlainNullableSchemaTransformer>();
        options.AddSchemaTransformer<FloatingPointSchemaTransformer>();
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "base-server-side API",
            Version = "v1",
            Description = "API for base-server-side"
        });
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        using var migrationScope = app.Services.CreateScope();
        migrationScope.ServiceProvider.GetRequiredService<MainDatabaseContext>().Database.Migrate();
    }

    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    // One structured summary line per request (method, path, status, elapsed).
    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
		var mediaSettings = builder.Configuration.GetSection("Media").Get<MediaSettings>()
		?? throw new InvalidOperationException("Missing required 'Media' configuration section.");

		app.UseStaticFiles(new StaticFileOptions
		{
			FileProvider = new PhysicalFileProvider(mediaSettings.UploadFolder),
			RequestPath = "/media"
		});

		app.MapOpenApi();
	}

	else
	{
		app.UseHsts();
	}

	app.UseCors("SpaPolicy");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Liveness: process responds. Readiness: tagged dependency checks (DB) pass.
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

    Log.Information("Application starting up...");
    app.Run();


}
catch (Exception ex)
{
    Console.WriteLine($"FATAL ERROR: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    Log.Fatal(ex, "Application terminated unexpectedly");
    // A swallowed fatal error must not look like a clean exit to the orchestrator.
    Environment.ExitCode = 1;
}
finally
{
    Log.Information("Application shutting down...");
    Log.CloseAndFlush();
}

// Makes the implicit Program class visible to WebApplicationFactory<Program> in tests.
public partial class Program { }
