-- ================================================
-- МИГРАЦИЯ БАЗЫ ДАННЫХ: Добавление полей основного обмена веществ (BMR)
-- Дата: 2025-07-06
-- Описание: Добавление полей BasalMetabolicRate и MetabolicRateCategory в таблицу BodyScans
-- ================================================

-- 1. Добавление новых полей в таблицу BodyScans
ALTER TABLE "BodyScans" ADD COLUMN "BasalMetabolicRate" INTEGER NULL;
ALTER TABLE "BodyScans" ADD COLUMN "MetabolicRateCategory" TEXT NULL;

-- 2. Создание индекса для поиска по категории метаболизма
CREATE INDEX IF NOT EXISTS "IX_BodyScans_MetabolicRateCategory" ON "BodyScans" ("MetabolicRateCategory");

-- 3. Обновление существующих записей (опционально)
-- Можно установить значения по умолчанию для существующих записей
UPDATE "BodyScans" 
SET "BasalMetabolicRate" = 1500, 
    "MetabolicRateCategory" = 'Нормальный'
WHERE "BasalMetabolicRate" IS NULL;

-- 4. Проверка добавления полей
PRAGMA table_info(BodyScans);

-- 5. Тестовый запрос для проверки новых полей
SELECT 
    "Id",
    "UserId", 
    "Weight",
    "BasalMetabolicRate",
    "MetabolicRateCategory",
    "ScanDate",
    "CreatedAt"
FROM "BodyScans" 
ORDER BY "ScanDate" DESC 
LIMIT 5;

-- ================================================
-- РЕЗУЛЬТАТ: 
-- ✅ Добавлены поля BasalMetabolicRate (INTEGER) и MetabolicRateCategory (TEXT)
-- ✅ Создан индекс для поиска по категории метаболизма
-- ✅ Обновлены существующие записи значениями по умолчанию
-- ================================================