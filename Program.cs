using FitnessTracker.API.Data;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc; 
using System.Text;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    // Увеличиваем таймауты для контроллеров
    options.ModelValidatorProviders.Clear();
})
.ConfigureApiBehaviorOptions(options =>
{
    // Более детальная обработка ошибок валидации
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
        Title = "🏃‍♂️ Fitness Tracker API",
        Version = "v2.1.0",
        Description = "Полнофункциональный API для фитнес-трекера"
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
        sqliteOptions.CommandTimeout(30); // Увеличиваем таймаут команд
    });

    // Включаем более детальное логирование для отладки
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddHttpClient();

// Services с увеличенными таймаутами
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

// Repositories
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

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-key-that-is-at-least-32-characters-long";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero // Убираем смещение времени
        };

        // ✅ ДОБАВЛЕНО: Улучшенная обработка событий JWT
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated successfully");
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
            .WithExposedHeaders("*"); // Разрешаем все заголовки в ответах
    });

    // Дополнительная политика для локальной разработки
    options.AddPolicy("Development", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => true) // Разрешаем любые источники в разработке
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Разрешаем credentials
    });
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 30000000; // 30MB
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 30000000; // 30MB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Проверяем подключение к базе данных
        await context.Database.EnsureCreatedAsync();

        // Проверяем, что база данных доступна
        await context.Database.CanConnectAsync();

        Console.WriteLine("✅ Database initialized successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database initialization error: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");

        // Не останавливаем приложение, но логируем ошибку
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "🏃‍♂️ Fitness Tracker API v2.1.0");
        c.RoutePrefix = "swagger"; // Явно указываем префикс
        c.DefaultModelsExpandDepth(-1);
        c.DisplayRequestDuration();
        c.EnableFilter();
        c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        c.EnableDeepLinking();
        c.ShowExtensions();
    });

    // Используем более мягкую CORS политику для разработки
    app.UseCors("Development");
}
else
{
    app.UseCors("AllowAll");
}

app.Use(async (context, next) =>
{
    var start = DateTime.UtcNow;

    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Request failed: {context.Request.Method} {context.Request.Path} - {ex.Message}");
        throw;
    }
    finally
    {
        var elapsed = DateTime.UtcNow - start;
        if (elapsed.TotalMilliseconds > 1000) // Логируем медленные запросы
        {
            Console.WriteLine($"⏰ Slow request: {context.Request.Method} {context.Request.Path} took {elapsed.TotalMilliseconds}ms");
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "60170";
var url = $"http://0.0.0.0:{port}";

Console.WriteLine("🚀 Fitness Tracker API starting...");
Console.WriteLine($"📊 Swagger: {url}/swagger");
Console.WriteLine($"🌐 API: {url}");
Console.WriteLine($"📚 Docs: {url}/api/docs");

try
{
    app.Run(url);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to start on port {port}: {ex.Message}");

    // Пробуем альтернативные порты
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