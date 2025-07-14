-- ================================================
-- МИГРАЦИЯ БАЗЫ ДАННЫХ: Обновление ценовой модели
-- Дата: 2025-07-13
-- Описание: Изменение стоимости AI операций согласно новой экономической модели
-- ================================================

-- 1. Добавление новых полей для отслеживания дробных монет
ALTER TABLE "LwCoinTransactions" ADD COLUMN "FractionalAmount" REAL DEFAULT 0.0;
ALTER TABLE "Users" ADD COLUMN "FractionalLwCoins" REAL DEFAULT 0.0;

-- 2. Обновление существующих данных - конвертация целых монет в дробные
UPDATE "Users" SET "FractionalLwCoins" = CAST("LwCoins" AS REAL);

-- 3. Обновление существующих транзакций
UPDATE "LwCoinTransactions" SET "FractionalAmount" = CAST("Amount" AS REAL);

-- 4. Создание индексов для новых полей
CREATE INDEX IF NOT EXISTS "IX_LwCoinTransactions_FractionalAmount" ON "LwCoinTransactions" ("FractionalAmount");
CREATE INDEX IF NOT EXISTS "IX_LwCoinTransactions_FeatureUsed_CreatedAt" ON "LwCoinTransactions" ("FeatureUsed", "CreatedAt");

-- 5. Добавление поля для отслеживания использования по дням
ALTER TABLE "LwCoinTransactions" ADD COLUMN "UsageDate" TEXT DEFAULT (DATE('now'));

-- 6. Обновление существующих записей - устанавливаем дату использования
UPDATE "LwCoinTransactions" 
SET "UsageDate" = DATE("CreatedAt") 
WHERE "UsageDate" IS NULL;

-- 7. Создание индекса для быстрого поиска по дате использования
CREATE INDEX IF NOT EXISTS "IX_LwCoinTransactions_UserId_UsageDate" ON "LwCoinTransactions" ("UserId", "UsageDate");

-- 8. Тестовая проверка структуры
PRAGMA table_info(LwCoinTransactions);
PRAGMA table_info(Users);

-- 9. Проверочный запрос для валидации данных
SELECT 
    "Id",
    "UserId", 
    "Amount",
    "FractionalAmount",
    "FeatureUsed",
    "UsageDate",
    "CreatedAt"
FROM "LwCoinTransactions" 
WHERE "Type" = 'spent'
ORDER BY "CreatedAt" DESC 
LIMIT 5;

-- ================================================
-- РЕЗУЛЬТАТ: 
-- ✅ Добавлены поля FractionalAmount и FractionalLwCoins для дробных монет
-- ✅ Добавлено поле UsageDate для отслеживания дневного использования
-- ✅ Созданы индексы для быстрого поиска
-- ✅ Обновлены существующие данные
-- ================================================