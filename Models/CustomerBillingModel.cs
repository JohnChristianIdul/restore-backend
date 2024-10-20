namespace Restore_backend_deployment_.Models
{
    public class CustomerCredits
    {
        public string Email { get; set; }
        public int CreditsRemaining { get; set; }
    }

    public class PaymentReceipt
    {
        public string Email { get; set; }
        public string CheckoutSessionId { get; set; }
        public DateTime PaymentDate { get; set; }
        public int Amount { get; set; } // Store amount in the smallest currency unit (e.g., cents)
        public string Description { get; set; }
    }

}
