using Newtonsoft.Json;

namespace Restore_backend_deployment_.Models
{
    public class PayMongoCheckoutSessionResponse
    {
        [JsonProperty("data")]
        public PayMongoCheckoutSessionData Data { get; set; }
    }

    public class PayMongoCheckoutSessionData
    {
        [JsonProperty("attributes")]
        public PayMongoCheckoutSessionAttributes Attributes { get; set; }
    }

    public class PayMongoCheckoutSessionAttributes
    {
        [JsonProperty("checkout_url")]
        public string CheckoutUrl { get; set; }
    }
}
