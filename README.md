# 🏃‍♂️ Fitness Tracker API

Полная документация RESTful API для фитнес-приложения с AI функциями, системой монетизации и геймификацией.

## 📋 Оглавление

- [Обзор проекта](#-обзор-проекта)
- [Архитектура системы](#️-архитектура-системы)
- [Установка и настройка](#-установка-и-настройка)
- [Структура проекта](#-структура-проекта)
- [База данных](#-база-данных)
- [API Endpoints](#-api-endpoints)
- [Система монет (LW Coins)](#-система-монет-lw-coins)
- [AI функционал](#-ai-функционал)
- [Деплой на сервер](#-деплой-на-сервер)
- [Важные скрипты](#-важные-скрипты)
- [Решение проблем](#-решение-проблем)
- [Контакты и поддержка](#-контакты-и-поддержка)

## 🎯 Обзор проекта

Fitness Tracker API - это RESTful API для фитнес-приложения с AI функциями, системой монетизации и геймификацией.

### Основные возможности:

- 📊 Трекинг питания и активностей
- 🤖 AI анализ (фото еды, голосовой ввод, анализ тела)
- 💰 Система LW Coins (внутренняя валюта)
- 🎮 Геймификация (уровни, опыт, скины, миссии)
- 🎯 Цели и прогресс
- 👥 Реферальная система (2 уровня)
- 🌍 Мультиязычность (15 языков)

### Технологический стек:

- **.NET 8.0** - основной фреймворк
- **SQLite** - база данных
- **Entity Framework Core** - ORM
- **Google Vertex AI (Gemini 2.5 Flash)** - AI анализ
- **JWT** - аутентификация
- **Swagger** - документация API

## 🏗️ Архитектура системы

Слоистая архитектура:

```
┌─────────────────────────────────────┐
│ Controllers Layer                   │ ← API endpoints
├─────────────────────────────────────┤
│ Services Layer                      │ ← Бизнес-логика
├─────────────────────────────────────┤
│ Repositories Layer                  │ ← Работа с данными
├─────────────────────────────────────┤
│ Data Layer (EF Core)                │ ← ORM
├─────────────────────────────────────┤
│ SQLite Database                     │ ← Хранилище
└─────────────────────────────────────┘
```

### Основные компоненты:

- **Controllers** - обработка HTTP запросов
- **Services** - бизнес-логика
- **Repositories** - доступ к данным
- **Models** - сущности базы данных
- **DTOs** - объекты передачи данных
- **Mapping** - маппинг между Models и DTOs

## 🚀 Установка и настройка

### Требования:

- .NET 8.0 SDK
- Visual Studio 2022 / VS Code
- Git
- PowerShell (для скриптов деплоя)

### Шаг 1: Клонирование репозитория

```bash
git clone https://github.com/your-repo/fitness-tracker-api.git
cd fitness-tracker-api
```

### Шаг 2: Настройка appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=fitness.db"
  },
  "BaseUrl": "http://localhost:60170",
  "GoogleCloud": {
    "ProjectId": "your-project-id",
    "ServiceAccountPath": "path-to-service-account.json"
  }
}
```

### Шаг 3: Установка зависимостей

```bash
dotnet restore
```

### Шаг 4: Создание базы данных

```bash
dotnet ef database update
```

### Шаг 5: Запуск проекта

```bash
dotnet run
```

**Swagger UI доступен по адресу:** http://localhost:60170/swagger

## 📁 Структура проекта

```
FitnessTracker.API/
├── Controllers/                   # HTTP контроллеры
│   ├── AIController.cs            # AI функции (фото, голос, текст)
│   ├── ActivityController.cs      # Тренировки и шаги
│   ├── AuthController.cs          # Аутентификация
│   ├── BodyScanController.cs      # Анализ тела
│   ├── FoodIntakeController.cs    # Питание
│   ├── GoalController.cs          # Цели
│   ├── LwCoinController.cs        # Монеты
│   ├── MissionController.cs       # Миссии
│   ├── ReferralController.cs      # Рефералы
│   ├── SkinController.cs          # Скины
│   ├── StatsController.cs         # Статистика
│   └── UserController.cs          # Профиль пользователя
│
├── Services/                      # Бизнес-логика
│   ├── AI/                        # AI сервисы
│   │   ├── UniversalAIService.cs  # Универсальный AI
│   │   ├── GoogleCloudTokenService.cs # Токены Google
│   │   └── Providers/
│   │       └── VertexAIProvider.cs # Vertex AI (Gemini)
│   ├── ActivityService.cs
│   ├── AuthService.cs
│   ├── BodyScanService.cs
│   ├── EmailService.cs
│   ├── ExperienceService.cs
│   ├── FoodIntakeService.cs
│   ├── GoalService.cs
│   ├── GoogleAuthService.cs
│   ├── ImageService.cs
│   ├── LocalizationService.cs
│   ├── LwCoinService.cs           # ⭐ ВАЖНО: Логика монет
│   ├── MissionService.cs
│   ├── ReferralService.cs
│   ├── SkinService.cs
│   ├── StatsService.cs
│   ├── UserService.cs
│   └── VoiceFileService.cs
│
├── Repositories/                  # Доступ к данным
│   ├── ActivityRepository.cs
│   ├── BodyScanRepository.cs
│   ├── FoodIntakeRepository.cs
│   ├── GoalRepository.cs
│   ├── LwCoinRepository.cs
│   ├── MissionRepository.cs
│   ├── ReferralRepository.cs
│   ├── SkinRepository.cs
│   └── UserRepository.cs
│
├── Models/                        # Сущности БД
│   ├── User.cs
│   ├── Activity.cs
│   ├── FoodIntake.cs
│   ├── LwCoinTransaction.cs
│   ├── Goal.cs
│   ├── Mission.cs
│   ├── Skin.cs
│   └── Referral.cs
│
├── DTOs/                          # Объекты передачи данных
│   ├── UserDto.cs
│   ├── ActivityDto.cs
│   ├── FoodIntakeDto.cs
│   └── ...
│
├── Data/
│   └── ApplicationDbContext.cs    # EF Core контекст
│
├── Mapping/
│   └── MappingProfile.cs          # AutoMapper профиль
│
├── Properties/
│   └── launchSettings.json        # Настройки запуска
│
├── wwwroot/
│   └── uploads/                   # Загруженные файлы
│       ├── food-scans/
│       ├── body-scans/
│       └── voice-files/
│
├── Program.cs                    # ⭐ Точка входа
├── appsettings.json              # ⭐ Конфигурация
├── FitnessTracker.API.csproj
└── fitness.db                    # SQLite база данных
```

## 💾 База данных

### Основные таблицы:

#### Users (Пользователи)

```sql
CREATE TABLE Users (
    Id TEXT PRIMARY KEY,
    Email TEXT UNIQUE NOT NULL,
    Name TEXT,
    Level INTEGER DEFAULT 1,
    Experience INTEGER DEFAULT 0,
    LwCoins INTEGER DEFAULT 0,
    FractionalLwCoins REAL DEFAULT 0.0, -- Дробные монеты
    Weight DECIMAL(5,2),
    Height DECIMAL(5,2),
    Age INTEGER,
    Gender TEXT,
    ReferralCode TEXT UNIQUE,
    Locale TEXT DEFAULT 'ru_RU',
    JoinedAt DATETIME
);
```

#### LwCoinTransactions (Транзакции монет)

```sql
CREATE TABLE LwCoinTransactions (
    Id TEXT PRIMARY KEY,
    UserId TEXT REFERENCES Users(Id),
    Amount INTEGER,
    FractionalAmount REAL,
    Type TEXT, -- 'spent', 'earned', 'purchase', 'expired'
    CoinSource TEXT, -- 'subscription', 'permanent', 'referral', 'registration'
    Description TEXT,
    FeatureUsed TEXT,
    ExpiryDate DATETIME NULL,
    IsExpired BOOLEAN DEFAULT 0,
    CreatedAt DATETIME
);
```

#### Activities (Тренировки)

```sql
CREATE TABLE Activities (
    Id TEXT PRIMARY KEY,
    UserId TEXT REFERENCES Users(Id),
    Type TEXT, -- 'strength' или 'cardio'
    StartDate DATETIME,
    EndDate DATETIME,
    Calories INTEGER,
    ActivityData TEXT, -- JSON данные
    CreatedAt DATETIME
);
```

#### FoodIntakes (Питание)

```sql
CREATE TABLE FoodIntakes (
    Id TEXT PRIMARY KEY,
    UserId TEXT REFERENCES Users(Id),
    Name TEXT,
    Weight DECIMAL(8,2),
    WeightType TEXT, -- 'g' или 'ml'
    DateTime DATETIME,
    -- NutritionPer100g хранится как JSON
);
```

## 🔌 API Endpoints

### Аутентификация

```http
POST /api/auth/send-code      # Отправка кода на email
POST /api/auth/confirm-email  # Подтверждение email
POST /api/auth/google         # Google OAuth
POST /api/auth/apple          # Apple Sign In
```

### Профиль пользователя

```http
GET  /api/user/profile        # Получить профиль
PUT  /api/user/profile        # Обновить профиль
POST /api/user/locale         # Установить язык
```

### AI функции ⭐

```http
POST /api/ai/scan-food        # Анализ фото еды (1 монета)
POST /api/ai/voice-food       # Голосовой ввод еды (1 монета)
POST /api/ai/text-food        # Текстовый ввод еды (БЕСПЛАТНО)
POST /api/ai/voice-workout    # Голосовая тренировка (1 монета)
POST /api/ai/text-workout     # Текстовая тренировка (БЕСПЛАТНО)
POST /api/ai/analyze-body     # Анализ тела (БЕСПЛАТНО)
POST /api/ai/correct-food     # Коррекция еды (БЕСПЛАТНО)
```

### LW Coins 💰

```http
GET  /api/lw-coin/balance     # Баланс монет
POST /api/lw-coin/spend       # Потратить монеты
POST /api/lw-coin/set-balance # Установить баланс (админ)
POST /api/lw-coin/purchase-subscription # Купить подписку
GET  /api/lw-coin/transactions # История транзакций
GET  /api/lw-coin/pricing     # Цены на функции
```

### Питание и активности

```http
GET  /api/food-intake         # Список приемов пищи
POST /api/food-intake         # Добавить еду
GET  /api/activity            # Список тренировок
POST /api/activity            # Добавить тренировку
POST /api/activity/steps      # Добавить шаги
```

### Цели

```http
GET  /api/goals               # Все цели
POST /api/goals               # Создать цель
GET  /api/goals/active        # Активная цель
GET  /api/goals/progress/today # Прогресс за сегодня
```

## 💰 Система монет (LW Coins)

### Цены на функции:

```csharp
// Services/LwCoinService.cs
private const decimal PHOTO_FOOD_SCAN_COST = 1.0m;  // Фото еды
private const decimal VOICE_FOOD_SCAN_COST = 1.0m;  // Голос еды
private const decimal TEXT_FOOD_SCAN_COST = 0.0m;   // Текст еды (бесплатно)
private const decimal VOICE_WORKOUT_COST = 1.0m;    // Голос тренировка
private const decimal TEXT_WORKOUT_COST = 0.0m;     // Текст тренировка (бесплатно)
private const decimal BODY_ANALYSIS_COST = 0.0m;    // Анализ тела (бесплатно)
```

### Изменение цен:

1. Откройте `Services/LwCoinService.cs`
2. Измените константы в начале файла
3. Пересоберите проект: `dotnet build`
4. Задеплойте на сервер

### Бонусы:

```csharp
private const int REGISTRATION_BONUS = 50;   // При регистрации
private const int REFERRAL_BONUS = 150;      // За реферала (1-й уровень)
// 2-й уровень получает 50% от REFERRAL_BONUS = 75 монет
```

### Типы монет:

- **permanent** - постоянные (покупка, бонусы)
- **subscription** - подписочные (истекают)
- **referral** - реферальные
- **registration** - регистрационные

## 🤖 AI функционал

### Настройка Vertex AI (Google Gemini):

#### 1. Создание Service Account:

```bash
# В Google Cloud Console:
# 1. Перейдите в IAM & Admin → Service Accounts
# 2. Create Service Account
# 3. Роли: "Vertex AI User", "Storage Object Viewer"
# 4. Create Key → JSON
# 5. Скачайте файл ключа
```

#### 2. Конфигурация в appsettings.json:

```json
{
  "GoogleCloud": {
    "ProjectId": "your-project-id",
    "Location": "us-central1",
    "Model": "gemini-2.5-flash",
    "ServiceAccountPath": "path-to-key.json"
  }
}
```

#### 3. Переключение AI провайдера:

```csharp
// Program.cs
builder.Services.AddScoped<IAIProvider, VertexAIProvider>();
// Можно заменить на другой провайдер при необходимости
```

### Добавление нового AI провайдера:

1. Создайте класс в `Services/AI/Providers/`:

```csharp
public class YourAIProvider : IAIProvider
{
    public string ProviderName => "Your AI";
    
    public async Task<FoodScanResponse> AnalyzeFoodImageAsync(
        byte[] imageData, string? userPrompt, string? locale)
    {
        // Ваша реализация
    }
    
    // ... остальные методы
}
```

2. Зарегистрируйте в `Program.cs`:

```csharp
builder.Services.AddScoped<IAIProvider, YourAIProvider>();
```

## 🚀 Деплой на сервер

### Автоматический деплой (PowerShell):

#### Безопасный деплой (сохраняет данные):

```powershell
# Запустите из папки проекта:
.\deploy-safe.ps1
```

Этот скрипт:
- ✅ Сохраняет базу данных
- ✅ Сохраняет загруженные файлы
- ✅ Обновляет только код
- ✅ Автоматически перезапускает сервис

### Ручной деплой:

#### 1. Сборка проекта:

```bash
dotnet publish -c Release -o publish
```

#### 2. Копирование на сервер:

```bash
scp -r publish/* root@80.90.183.10:/var/www/fitness-tracker-api/
```

#### 3. Настройка systemd сервиса:

```bash
# /etc/systemd/system/fitness-tracker-api.service
[Unit]
Description=Fitness Tracker API
After=network.target

[Service]
Type=simple
User=www-data
Group=www-data
WorkingDirectory=/var/www/fitness-tracker-api/publish
ExecStart=/usr/bin/dotnet FitnessTracker.API.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:60170

[Install]
WantedBy=multi-user.target
```

#### 4. Запуск сервиса:

```bash
systemctl daemon-reload
systemctl enable fitness-tracker-api
systemctl start fitness-tracker-api
systemctl status fitness-tracker-api
```

### Информация о сервере:

- **IP:** 80.90.183.10
- **Port:** 60170
- **API URL:** http://80.90.183.10:60170
- **Swagger:** http://80.90.183.10:60170/swagger
- **OS:** Ubuntu 22.04
- **Path:** /var/www/fitness-tracker-api/

## 📜 Важные скрипты

### deploy-safe.ps1 (Безопасный деплой)

```powershell
# Основные шаги:
# 1. Создает архив только с кодом (без БД)
# 2. Загружает на сервер через pscp
# 3. Делает бэкап старого кода
# 4. Распаковывает новый код
# 5. Сохраняет БД и uploads
# 6. Пересобирает проект
# 7. Перезапускает сервис
```

### Миграция базы данных:

```bash
# Создание новой миграции
dotnet ef migrations add MigrationName

# Применение миграции
dotnet ef database update

# На сервере (если нужно)
cd /var/www/fitness-tracker-api
dotnet ef database update --project FitnessTracker.API.csproj
```

### Бэкап базы данных:

```bash
# На сервере
cp /var/www/fitness-tracker-api/publish/fitness.db \
   /backups/fitness-$(date +%Y%m%d-%H%M%S).db
```

### Просмотр логов:

```bash
# Последние 100 строк логов
journalctl -u fitness-tracker-api -n 100

# Логи в реальном времени
journalctl -u fitness-tracker-api -f

# Логи за последний час
journalctl -u fitness-tracker-api --since "1 hour ago"
```

## 🔧 Решение проблем

### Проблема: Сервис не запускается

```bash
# Проверьте статус
systemctl status fitness-tracker-api

# Проверьте логи
journalctl -u fitness-tracker-api -n 50

# Проверьте права
chown -R www-data:www-data /var/www/fitness-tracker-api
chmod -R 755 /var/www/fitness-tracker-api
```

### Проблема: База данных заблокирована

```bash
# Остановите сервис
systemctl stop fitness-tracker-api

# Удалите lock файлы
rm /var/www/fitness-tracker-api/publish/fitness.db-shm
rm /var/www/fitness-tracker-api/publish/fitness.db-wal

# Запустите снова
systemctl start fitness-tracker-api
```

### Проблема: AI не работает

```bash
# Проверьте service account
ls -la /var/www/fitness-tracker-api/publish/*.json

# Проверьте права
chmod 644 /var/www/fitness-tracker-api/publish/quick-nexus*.json

# Проверьте конфигурацию
cat /var/www/fitness-tracker-api/publish/appsettings.json | grep GoogleCloud
```

### Проблема: Недостаточно места на диске

```bash
# Проверьте место
df -h

# Очистите старые бэкапы
rm /var/backups/fitness-tracker/code-backup-2024*

# Очистите старые логи
journalctl --vacuum-time=7d
```

## 🔒 Безопасность

### JWT токены:

- **Срок жизни:** 30 дней
- **Секретный ключ** в Program.cs
- **Алгоритм:** HmacSha256

### Изменение JWT ключа:

```csharp
// Program.cs
private static readonly string JWT_SECRET_KEY = "your-new-secret-key-minimum-32-characters";
```

### Права доступа:

- Все endpoints требуют авторизации (кроме `/api/health`)
- Админские функции проверяют роли (в разработке)

## 📊 Мониторинг

### Проверка здоровья API:

```http
GET /api/health
```

**Ответ:**

```json
{
  "status": "healthy",
  "database": {
    "canConnect": true,
    "userCount": 150,
    "lastActivity": "2025-01-15T10:30:00Z"
  },
  "timestamp": "2025-01-15T12:00:00Z"
}
```

### Статистика AI:

```http
GET /api/ai/status
```

### Метрики базы данных:

```http
GET /api/health/db-info
```

## 🌍 Локализация

### Поддерживаемые языки:

- **ru** - Русский
- **en** - English
- **es** - Español
- **de** - Deutsch
- **fr** - Français
- **zh** - 中文
- **ja** - 日本語
- **ko** - 한국어
- **pt** - Português
- **it** - Italiano
- **ar** - العربية
- **hi** - हिन्दी
- **tr** - Türkçe
- **pl** - Polski
- **uk** - Українська

### Добавление нового языка:

1. Откройте `Services/LocalizationService.cs`
2. Добавьте язык в `_availableLanguages`
3. Добавьте переводы в `_translations`

## 📧 Email настройка

### SMTP конфигурация (Gmail):

```json
{
  "Email": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "noreply@lightweightfit.com",
    "Password": "app-specific-password",
    "EnableSsl": true
  }
}
```

### Создание App Password для Gmail:

1. Включите 2FA в Google Account
2. Перейдите в Security → App passwords
3. Создайте пароль для "Mail"
4. Используйте его в конфигурации

## 🔄 Обновления

### Добавление новой функции:

1. Создайте Controller в `Controllers/`
2. Создайте Service в `Services/`
3. Создайте Repository в `Repositories/`
4. Добавьте Model в `Models/`
5. Добавьте DTO в `DTOs/`
6. Обновите `MappingProfile.cs`
7. Добавьте в `ApplicationDbContext.cs`
8. Создайте миграцию: `dotnet ef migrations add NewFeature`

### Изменение цен на монеты:

```csharp
// Services/LwCoinService.cs
// Измените константы в начале файла
private const decimal PHOTO_FOOD_SCAN_COST = 2.0m; // Было 1.0
```

### Изменение бонусов:

```csharp
// Services/LwCoinService.cs
private const int REGISTRATION_BONUS = 100; // Было 50
private const int REFERRAL_BONUS = 200;     // Было 150
```

## 🐛 Отладка

### Включение подробных логов:

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "FitnessTracker.API.Services.AI": "Debug"
    }
  }
}
```

### Тестовые аккаунты:

- **Email:** test@lightweightfit.com **Code:** 123456
- **Email:** demo@lightweightfit.com **Code:** 111111
- **Email:** review@lightweightfit.com **Code:** 777777

## 📞 Контакты и поддержка

### Структура команды:

- **Backend:** .NET 8.0, C#, Entity Framework
- **AI Integration:** Google Vertex AI, Gemini 2.5 Flash
- **Database:** SQLite, Entity Framework Core
- **DevOps:** Ubuntu, systemd, nginx

### При возникновении вопросов:

1. Проверьте эту документацию
2. Проверьте Swagger: http://80.90.183.10:60170/swagger
3. Проверьте логи: `journalctl -u fitness-tracker-api -f`
4. Проверьте код в соответствующем Service/Controller

### Важные файлы для изучения:

- `Program.cs` - конфигурация приложения
- `Services/LwCoinService.cs` - логика монет
- `Services/AI/UniversalAIService.cs` - AI интеграция
- `Controllers/AIController.cs` - AI endpoints
- `Data/ApplicationDbContext.cs` - структура БД

## 📝 Заметки для будущих разработчиков

### Что важно знать:

- **Монеты** - вся логика в `LwCoinService.cs`
- **AI** - можно заменить провайдера в `Program.cs`
- **База данных** - SQLite, файл `fitness.db`
- **Деплой** - используйте `deploy-safe.ps1` для безопасного обновления
- **Логи** - `journalctl -u fitness-tracker-api -f`
- **Бэкапы** - делайте перед важными изменениями

### Частые задачи:

#### Изменить цену функции:

```csharp
// Services/LwCoinService.cs
private const decimal PHOTO_FOOD_SCAN_COST = 2.0m; // Изменить здесь
```

#### Добавить новый язык:

```csharp
// Services/LocalizationService.cs
// Добавить в _availableLanguages и _translations
```

#### Изменить AI модель:

```json
// appsettings.json
"GoogleCloud": {
  "Model": "gemini-pro" // Изменить здесь
}
```

#### Увеличить лимит загрузки файлов:

```csharp
// Program.cs
builder.WebHost.ConfigureKestrel(options => {
    options.Limits.MaxRequestBodySize = 100_000_000; // 100MB
});
```

## ✅ Чек-лист перед деплоем

- [ ] Проверен `appsettings.json`
- [ ] Сделан бэкап БД
- [ ] Проверены миграции
- [ ] Протестирован локально
- [ ] Проверен service account для AI
- [ ] Подготовлен скрипт деплоя

## 🎯 Итоговая памятка

### Самое важное:

- **База данных** - `fitness.db` (SQLite)
- **Монеты** - `Services/LwCoinService.cs`
- **AI** - `Services/AI/Providers/VertexAIProvider.cs`
- **Деплой** - `deploy-safe.ps1`
- **Сервер** - 80.90.183.10:60170
- **Swagger** - http://80.90.183.10:60170/swagger

### При проблемах:

- Смотрите логи: `journalctl -u fitness-tracker-api -f`
- Проверяйте права: `chown -R www-data:www-data /var/www/fitness-tracker-api`
- Перезапускайте: `systemctl restart fitness-tracker-api`

---

**Документация создана:** Сентябрь 2025  
**Версия API:** 3.0.0  
**Последнее обновление:** с Gemini 2.5 Flash