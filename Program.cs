using FitnessTracker.API.Data;
using FitnessTracker.API.Services;
using FitnessTracker.API.Services.AI;
using FitnessTracker.API.Services.AI.Providers;
using FitnessTracker.API.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Reflection;
using System.Security.Claims;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);

    logging.AddFilter("FitnessTracker.API.Services.AI", LogLevel.Debug);
    logging.AddFilter("FitnessTracker.API.Controllers.AIController", LogLevel.Debug);
});

builder.Services.AddControllers(options =>
{
    options.ModelValidatorProviders.Clear();
})
.ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
            );

        return new BadRequestObjectResult(new { errors });
    };
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "🏃‍♂️ Fitness Tracker API с Universal AI",
        Version = "v3.0.0",
        Description = "Полнофункциональный API для фитнес-трекера с универсальной AI архитектурой (Vertex AI Gemini 2.5 Flash + OpenAI + другие)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Введите 'Bearer' [пробел] и затем ваш токен.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.EnableAnnotations();
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite("Data Source=fitness.db", sqliteOptions =>
    {
        sqliteOptions.CommandTimeout(30);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddHttpClient<VertexAIProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Add("User-Agent", "FitnessTracker-API/3.0.0");
});

builder.Services.AddScoped<IGoogleCloudTokenService, GoogleCloudTokenService>();
builder.Services.AddScoped<IAIProvider, VertexAIProvider>();

builder.Services.AddScoped<IAIErrorHandlerService, AIErrorHandlerService>();

builder.Services.AddScoped<IGeminiService, UniversalAIService>();

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFoodIntakeService, FoodIntakeService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<ISkinService, SkinService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IMissionService, MissionService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<ILwCoinService, LwCoinService>();
builder.Services.AddScoped<IBodyScanService, BodyScanService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddScoped<IExperienceService, ExperienceService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IVoiceFileService, VoiceFileService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFoodIntakeRepository, FoodIntakeRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<ISkinRepository, SkinRepository>();
builder.Services.AddScoped<IReferralRepository, ReferralRepository>();
builder.Services.AddScoped<IMissionRepository, MissionRepository>();
builder.Services.AddScoped<ILwCoinRepository, LwCoinRepository>();
builder.Services.AddScoped<IBodyScanRepository, BodyScanRepository>();
builder.Services.AddScoped<IAchievementRepository, AchievementRepository>();
builder.Services.AddScoped<IExperienceRepository, ExperienceRepository>();
builder.Services.AddScoped<IStepsRepository, StepsRepository>();
builder.Services.AddScoped<IGoalRepository, GoalRepository>();

const string JWT_SECRET_KEY = "fitness-tracker-super-secret-key-that-is-definitely-long-enough-for-security-2024";
var key = Encoding.UTF8.GetBytes(JWT_SECRET_KEY);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = "FitnessTracker",
            ValidateAudience = true,
            ValidAudience = "FitnessTracker",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            RequireExpirationTime = true
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError($"🔥 JWT Authentication failed: {context.Exception.Message}");

                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers["Token-Expired"] = "true";
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                logger.LogInformation($"✅ JWT Token validated successfully for user: {userId}");
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                if (!string.IsNullOrEmpty(token))
                {
                    logger.LogDebug($"📩 JWT Token received: {token[..Math.Min(20, token.Length)]}...");
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning($"🚫 JWT Challenge triggered: {context.Error} - {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("*");
    });

    options.AddPolicy("Development", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });

    // ✅ НОВАЯ ПОЛИТИКА для поддомена
    options.AddPolicy("Production", policy =>
    {
        policy
            .WithOrigins(
                "https://lightweightfit.com",
                "http://lightweightfit.com",
                "https://api.lightweightfit.com",
                "http://api.lightweightfit.com",
                "https://www.lightweightfit.com",
                "http://www.lightweightfit.com"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("*");
    });
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 50000000;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50000000;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(60);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var universalAI = scope.ServiceProvider.GetRequiredService<IGeminiService>() as UniversalAIService;
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        Console.WriteLine("🤖 Universal AI Configuration:");
        Console.WriteLine($"   Active Provider: {configuration["AI:ActiveProvider"] ?? "Vertex AI (Gemini 2.5 Flash)"}");
        Console.WriteLine($"   Google Cloud Project: {configuration["GoogleCloud:ProjectId"] ?? "NOT SET"}");
        Console.WriteLine($"   Google Cloud Location: {configuration["GoogleCloud:Location"] ?? "us-central1"}");
        Console.WriteLine($"   Google Cloud Model: {configuration["GoogleCloud:Model"] ?? "gemini-2.5-flash"}");

        var tokenService = scope.ServiceProvider.GetRequiredService<IGoogleCloudTokenService>();
        var isValidServiceAccount = await tokenService.ValidateServiceAccountAsync();

        if (isValidServiceAccount)
        {
            Console.WriteLine("✅ Google Cloud service account validated successfully");
        }
        else
        {
            Console.WriteLine("⚠️ WARNING: Google Cloud service account validation failed!");
        }

        if (universalAI != null)
        {
            var healthStatus = await universalAI.GetProviderHealthStatusAsync();
            Console.WriteLine("🏥 AI Providers Health Status:");
            foreach (var provider in healthStatus)
            {
                var status = provider.Value ? "✅ HEALTHY" : "❌ UNHEALTHY";
                Console.WriteLine($"   {provider.Key}: {status}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error checking AI configuration: {ex.Message}");
    }
}

using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Console.WriteLine("🗄️ Initializing database...");

        var databaseExists = await context.Database.CanConnectAsync();

        if (!databaseExists)
        {
            Console.WriteLine("🆕 Database does not exist, creating...");
            await context.Database.EnsureCreatedAsync();
            Console.WriteLine("✅ Database created successfully!");
        }
        else
        {
            Console.WriteLine("✅ Database already exists, checking connection...");

            var hasMigrationsTable = await context.Database.ExecuteSqlRawAsync(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';") > 0;

            if (!hasMigrationsTable)
            {
                Console.WriteLine("📦 No migrations table found, database is up to date.");
            }
            else
            {
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    Console.WriteLine($"📦 Applying {pendingMigrations.Count()} pending migrations...");
                    await context.Database.MigrateAsync();
                    Console.WriteLine("✅ Migrations applied successfully!");
                }
                else
                {
                    Console.WriteLine("✅ No pending migrations.");
                }
            }
        }

        Console.WriteLine("✅ Database initialized successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database initialization error: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        Console.WriteLine("⚠️ Continuing startup despite database error...");
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "🏃‍♂️ Fitness Tracker API v3.0.0 with Gemini 2.5 Flash");
    c.RoutePrefix = "swagger";
    c.DefaultModelsExpandDepth(-1);
    c.DisplayRequestDuration();
    c.EnableFilter();
    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    c.EnableDeepLinking();
    c.ShowExtensions();
});

if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("Production");
}

var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(Path.Combine(uploadsPath, "food-scans"));
Directory.CreateDirectory(Path.Combine(uploadsPath, "body-scans"));
Directory.CreateDirectory(Path.Combine(uploadsPath, "voice-workouts"));
Directory.CreateDirectory(Path.Combine(uploadsPath, "voice-food"));

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Add("Cache-Control", "public,max-age=3600");
    }
});

app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (!string.IsNullOrEmpty(authHeader))
    {
        logger.LogDebug($"🔑 Request with auth: {context.Request.Method} {context.Request.Path}");
    }

    if (context.Request.Path.StartsWithSegments("/api/ai"))
    {
        var contentLength = context.Request.ContentLength ?? 0;
        logger.LogInformation($"🤖 AI Request: {context.Request.Method} {context.Request.Path} (Size: {contentLength} bytes)");

        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();
        var contentType = context.Request.Headers["Content-Type"].FirstOrDefault();
        logger.LogDebug($"🤖 AI Request headers - Content-Type: {contentType}, User-Agent: {userAgent}");
    }

    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        logger.LogError($"❌ Request failed: {context.Request.Method} {context.Request.Path} - {ex.Message}");

        if (context.Request.Path.StartsWithSegments("/api/ai"))
        {
            logger.LogError($"🤖 AI Request failed - Path: {context.Request.Path}, Error: {ex.Message}");
            logger.LogError($"🤖 AI Request stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                logger.LogError($"🤖 AI Request inner exception: {ex.InnerException.Message}");
            }
        }
        throw;
    }
    finally
    {
        var elapsed = DateTime.UtcNow - start;

        if (elapsed.TotalMilliseconds > 1000)
        {
            logger.LogWarning($"⏰ Slow request: {context.Request.Method} {context.Request.Path} took {elapsed.TotalMilliseconds}ms");
        }

        if (context.Request.Path.StartsWithSegments("/api/ai"))
        {
            var statusCode = context.Response.StatusCode;
            var statusEmoji = statusCode >= 200 && statusCode < 300 ? "✅" : "❌";
            logger.LogInformation($"🤖 AI Request completed {statusEmoji} - Status: {statusCode}, Time: {elapsed.TotalMilliseconds}ms");

            if (elapsed.TotalSeconds > 30)
            {
                logger.LogWarning($"🤖⏰ Very slow AI request: {context.Request.Path} took {elapsed.TotalSeconds:F1} seconds");
            }
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "60170";
var url = $"http://0.0.0.0:{port}";

Console.WriteLine("🚀 Fitness Tracker API with Universal AI starting...");
Console.WriteLine("🤖 Now using Gemini 2.5 Flash (latest model)!");
Console.WriteLine($"📊 Swagger: {url}/swagger");
Console.WriteLine($"🌐 API: {url}");
Console.WriteLine($"📚 Docs: {url}/api/docs");
Console.WriteLine($"🤖 AI Status: {url}/api/ai/status");
Console.WriteLine($"🔑 JWT Secret: {JWT_SECRET_KEY[..20]}...");
Console.WriteLine("✅ Updated to Gemini 2.5 Flash - latest and greatest AI model!");

Console.WriteLine("");
Console.WriteLine("🌐 DOMAIN CONFIGURATION:");
Console.WriteLine($"   Primary Domain: lightweightfit.com → Vercel (Landing)");
Console.WriteLine($"   API Subdomain: api.lightweightfit.com:60170 → This Server");
Console.WriteLine($"   Email Domain: noreply@lightweightfit.com → Google Workspace SMTP");
Console.WriteLine($"   Swagger URL: http://api.lightweightfit.com:60170/swagger");
Console.WriteLine("✅ Professional domain setup complete!");

try
{
    app.Run(url);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to start on port {port}: {ex.Message}");

    var alternatePorts = new[] { "60176", "60177", "60178" };

    foreach (var altPort in alternatePorts)
    {
        try
        {
            Console.WriteLine($"🔄 Trying port {altPort}...");
            app.Run($"http://0.0.0.0:{altPort}");
            break;
        }
        catch (Exception ex2)
        {
            Console.WriteLine($"❌ Port {altPort} also failed: {ex2.Message}");
        }
    }
}