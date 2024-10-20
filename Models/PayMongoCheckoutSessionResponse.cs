using Newtonsoft.Json;

namespace Restore_backend_deployment_.Models
{
    public class CheckoutSessionRequest
    {
        public Data? Data { get; set; }
        public Int32 Credits { get; set; }
    }

    public class Data
    {
        public Attributes? Attributes { get; set; }
    }

    public class Attributes
    {
        public Billing? Billing { get; set; }
        public bool SendEmailReceipt { get; set; }
        public bool ShowDescription { get; set; }
        public bool ShowLineItems { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? StatementDescriptor { get; set; }
        public string? SuccessUrl { get; set; }
        public List<string>? PaymentMethodTypes { get; set; }
        public string? Description { get; set; }
        public string? CancelUrl { get; set; }
    }

    public class Billing
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

}
