-- Добавляем новые поля в таблицу Users
ALTER TABLE Users ADD COLUMN LwCoins INTEGER DEFAULT 150;
ALTER TABLE Users ADD COLUMN LastMonthlyRefill DATETIME DEFAULT CURRENT_TIMESTAMP;
ALTER TABLE Users ADD COLUMN HasPremiumSubscription BOOLEAN DEFAULT FALSE;
ALTER TABLE Users ADD COLUMN PremiumExpiresAt DATETIME NULL;
ALTER TABLE Users ADD COLUMN MonthlyLwCoinsUsed INTEGER DEFAULT 0;
ALTER TABLE Users ADD COLUMN CurrentMonthStart DATETIME DEFAULT CURRENT_TIMESTAMP;

-- Создаем таблицу LwCoinTransactions
CREATE TABLE IF NOT EXISTS LwCoinTransactions (
    Id TEXT PRIMARY KEY,
    UserId TEXT NOT NULL,
    Amount INTEGER NOT NULL,
    Type TEXT NOT NULL,
    Description TEXT NOT NULL,
    FeatureUsed TEXT DEFAULT '',
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- Создаем таблицу Subscriptions
CREATE TABLE IF NOT EXISTS Subscriptions (
    Id TEXT PRIMARY KEY,
    UserId TEXT NOT NULL,
    Type TEXT NOT NULL,
    Price DECIMAL(10,2) NOT NULL,
    Currency TEXT DEFAULT 'USD',
    PurchasedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    ExpiresAt DATETIME NULL,
    IsActive BOOLEAN DEFAULT TRUE,
    PaymentTransactionId TEXT DEFAULT '',
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- Обновляем существующих пользователей (даем им 150 стартовых LW Coins)
UPDATE Users SET 
    LwCoins = 150,
    LastMonthlyRefill = CURRENT_TIMESTAMP,
    HasPremiumSubscription = FALSE,
    MonthlyLwCoinsUsed = 0,
    CurrentMonthStart = CURRENT_TIMESTAMP
WHERE LwCoins IS NULL;