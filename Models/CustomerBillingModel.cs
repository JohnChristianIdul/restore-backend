﻿using Google.Cloud.Firestore;

namespace Restore_backend_deployment_.Models
{
    [FirestoreData]
    public class CustomerCredits
    {
        public CustomerCredits() { }
        [FirestoreProperty]
        public string? Email { get; set; }
        [FirestoreProperty]
        public int CreditsRemaining { get; set; }
    }

    [FirestoreData]
    public class PaymentReceipt
    {
        [FirestoreProperty]
        public string? Email { get; set; }
        [FirestoreProperty]
        public string? CheckoutSessionId { get; set; }
        [FirestoreProperty]
        public DateTime PaymentDate { get; set; }
        [FirestoreProperty]
        public int Amount { get; set; }
        [FirestoreProperty]
        public string? Description { get; set; }
    }

}
