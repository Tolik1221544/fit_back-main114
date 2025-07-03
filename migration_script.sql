-- ================================================
-- МИГРАЦИЯ БАЗЫ ДАННЫХ: Цели и обновленные активности
-- Дата: 2025-07-03
-- Описание: Добавление таблиц для целей и обновление силовых тренировок
-- ================================================

-- 1. Создание таблицы Goals (Цели)
CREATE TABLE IF NOT EXISTS "Goals" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Goals" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "GoalType" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "TargetWeight" NUMERIC(5,2),
    "CurrentWeight" NUMERIC(5,2),
    "TargetCalories" INTEGER,
    "TargetProtein" INTEGER,
    "TargetCarbs" INTEGER,
    "TargetFats" INTEGER,
    "TargetWorkoutsPerWeek" INTEGER,
    "TargetStepsPerDay" INTEGER,
    "TargetActiveMinutes" INTEGER,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "StartDate" TEXT NOT NULL,
    "EndDate" TEXT,
    "CreatedAt" TEXT NOT NULL,
    "CompletedAt" TEXT,
    "ProgressPercentage" NUMERIC(5,2) NOT NULL DEFAULT 0.0,
    "Status" TEXT NOT NULL DEFAULT 'active',
    CONSTRAINT "FK_Goals_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- 2. Создание таблицы DailyGoalProgress (Ежедневный прогресс по целям)
CREATE TABLE IF NOT EXISTS "DailyGoalProgress" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DailyGoalProgress" PRIMARY KEY,
    "GoalId" TEXT NOT NULL,
    "UserId" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "ActualCalories" INTEGER NOT NULL DEFAULT 0,
    "ActualProtein" NUMERIC(8,2) NOT NULL DEFAULT 0.0,
    "ActualCarbs" NUMERIC(8,2) NOT NULL DEFAULT 0.0,
    "ActualFats" NUMERIC(8,2) NOT NULL DEFAULT 0.0,
    "ActualSteps" INTEGER NOT NULL DEFAULT 0,
    "ActualWorkouts" INTEGER NOT NULL DEFAULT 0,
    "ActualActiveMinutes" INTEGER NOT NULL DEFAULT 0,
    "ActualWeight" NUMERIC(5,2),
    "CaloriesProgress" NUMERIC(5,2) NOT NULL DEFAULT 0.0,
    "ProteinProgress" NUMERIC(5,2) NOT NULL DEFAULT 0.0,
    "CarbsProgress" NUMERIC(5,2) NOT NULL DEFAULT 0.0,
    "FatsProgress" NUMERIC(5,2) NOT NULL DEFAULT 0.0,
    "StepsProgress" NUMERIC(5,2) NOT NULL DEFAULT 0.0,
    "WorkoutProgress" NUMERIC(5,2) NOT NULL DEFAULT 0.0,
    "OverallProgress" NUMERIC(5,2) NOT NULL DEFAULT 0.0,
    "IsCompleted" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_DailyGoalProgress_Goals_GoalId" FOREIGN KEY ("GoalId") REFERENCES "Goals" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DailyGoalProgress_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- 3. Создание индексов для производительности
CREATE INDEX IF NOT EXISTS "IX_Goals_UserId_IsActive" ON "Goals" ("UserId", "IsActive");
CREATE INDEX IF NOT EXISTS "IX_Goals_GoalType" ON "Goals" ("GoalType");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_DailyGoalProgress_UserId_GoalId_Date" ON "DailyGoalProgress" ("UserId", "GoalId", "Date");
CREATE INDEX IF NOT EXISTS "IX_DailyGoalProgress_Date" ON "DailyGoalProgress" ("Date");

-- 4. Добавление новых миссий для целей
INSERT OR IGNORE INTO "Missions" ("Id", "Title", "Icon", "RewardExperience", "Type", "TargetValue", "Route", "IsActive") VALUES
('mission_daily_goal_80', 'Выполни дневную цель на 80%', '🎯', 75, 'daily_goal_progress', 80, '/goals', 1),
('mission_weekly_goal_streak', 'Неделя выполнения целей', '🔥', 200, 'weekly_goal_streak', 7, '/goals', 1);

-- 5. Добавление новых достижений для целей
INSERT OR IGNORE INTO "Achievements" ("Id", "Title", "Icon", "ImageUrl", "Type", "RequiredValue", "RewardExperience", "IsActive") VALUES
('achievement_goal_setter', 'Постановщик целей', '🎯', 'https://example.com/achievements/goal-setter.png', 'goal_count', 1, 150, 1),
('achievement_goal_achiever', 'Достигатор целей', '🏆', 'https://example.com/achievements/goal-achiever.png', 'completed_goal_count', 1, 300, 1),
('achievement_consistency_master', 'Мастер постоянства', '🔥', 'https://example.com/achievements/consistency-master.png', 'goal_streak_days', 30, 500, 1);

-- 6. Обновление существующих записей (если нужно)
-- Примечание: Поле StrengthData в Activities уже поддерживает JSON,
-- поэтому новая структура с Sets будет работать автоматически

-- 7. Проверка создания таблиц
SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Goals', 'DailyGoalProgress');

-- ================================================
-- РЕЗУЛЬТАТ: 
-- ✅ Созданы таблицы Goals и DailyGoalProgress
-- ✅ Добавлены индексы для производительности  
-- ✅ Добавлены новые миссии и достижения
-- ✅ Поддержка новой структуры силовых тренировок
-- ================================================