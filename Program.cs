using FitnessTracker.API.Data;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger configuration with XML comments
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "🏃‍♂️ Fitness Tracker API",
        Version = "v2.1.0",
        Description = @"
## 📱 Полнофункциональный API для фитнес-трекера

### 🔥 Основные возможности:
- **🔐 Аутентификация** - Email + код подтверждения или Google OAuth
- **👤 Профиль** - Управление данными пользователя (имя, возраст, вес, рост)
- **🏃‍♂️ Активности** - Силовые и кардио тренировки с детальной статистикой
- **👣 Шаги** - Отслеживание ежедневной активности
- **🍎 Питание** - Запись приемов пищи с подсчетом калорий и БЖУ
- **💰 LW Coins** - Система внутренней валюты с лимитами и премиум подпиской
- **👥 Рефералы** - Двухуровневая реферальная программа
- **🎯 Миссии** - Система заданий и достижений
- **📸 Скан тела** - Отслеживание физических изменений

### 🔑 Аутентификация:
Используйте Bearer токен в заголовке: `Authorization: Bearer YOUR_TOKEN`

### 💡 Важные моменты для фронтенда:
- Все даты в формате ISO 8601 (UTC)
- Для активностей: заполняйте либо strengthData, либо cardioData
- Калории всегда опциональны, но рекомендуются
- LW Coins тратятся на premium функции (сканирование фото, голос)
",
        Contact = new OpenApiContact
        {
            Name = "Fitness Tracker API",
            Url = new Uri("https://github.com/fitness-tracker")
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header. 
                      Введите 'Bearer' [пробел] и затем ваш токен.
                      Пример: 'Bearer 12345abcdef'",
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

    // Включаем XML комментарии
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    // Показываем примеры в Swagger UI
    c.EnableAnnotations();
});

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=fitness.db"));

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFoodIntakeService, FoodIntakeService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
// builder.Services.AddScoped<ICoinService, CoinService>(); 
builder.Services.AddScoped<ISkinService, SkinService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IMissionService, MissionService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<ILwCoinService, LwCoinService>();
builder.Services.AddScoped<IBodyScanService, BodyScanService>();
builder.Services.AddScoped<IAchievementService, AchievementService>();
builder.Services.AddScoped<IExperienceService, ExperienceService>();

// Repositories (✅ УБРАЛИ ICoinRepository и CoinRepository)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFoodIntakeRepository, FoodIntakeRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
// builder.Services.AddScoped<ICoinRepository, CoinRepository>(); 
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
            ValidateAudience = false
        };
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Database initialization
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        Console.WriteLine("✅ Database created successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database error: {ex.Message}");
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "🏃‍♂️ Fitness Tracker API v2.1.0");
    c.DefaultModelsExpandDepth(-1);
    c.DisplayRequestDuration();
    c.EnableFilter();
    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    c.EnableDeepLinking();
    c.ShowExtensions();
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("🚀 Fitness Tracker API starting...");
Console.WriteLine($"📊 Swagger: http://localhost:60170/swagger");
Console.WriteLine($"🌐 API: http://localhost:60170");
Console.WriteLine($"📚 Docs: http://localhost:60170/api/docs");

app.Run();