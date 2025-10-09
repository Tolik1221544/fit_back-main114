namespace FitnessTracker.API.DTOs
{
    public class VerifyGooglePurchaseRequest
    {
        public string PurchaseToken { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string PackageType { get; set; } = "subscription"; 
    }

    public class VerifyApplePurchaseRequest
    {
        public string TransactionId { get; set; } = string.Empty;
        public string ReceiptData { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string PackageType { get; set; } = "subscription";
    }

    public class PurchaseVerificationResponse
    {
        public bool Success { get; set; }
        public string VerificationId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public int CoinsAdded { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsSubscription { get; set; }

        public int NewBalance { get; set; }

        public string? ErrorDetails { get; set; }
    }

    public class RestorePurchasesRequest
    {
        public string Platform { get; set; } = string.Empty; 

        public List<string>? PurchaseTokens { get; set; }

        public string? ReceiptData { get; set; }
    }

    public class RestorePurchasesResponse
    {
        public bool Success { get; set; }
        public int RestoredCount { get; set; }
        public List<RestoredPurchaseInfo> RestoredPurchases { get; set; } = new();
        public int TotalCoinsRestored { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class RestoredPurchaseInfo
    {
        public string ProductId { get; set; } = string.Empty;
        public int CoinsAmount { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsSubscription { get; set; }
    }

    public class SubscriptionStatusResponse
    {
        public bool HasActiveSubscription { get; set; }
        public string? ProductId { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int DaysRemaining { get; set; }
        public bool IsAutoRenewing { get; set; }
        public string Platform { get; set; } = string.Empty;
    }
}