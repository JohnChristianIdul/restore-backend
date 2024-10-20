namespace Restore_backend_deployment_.Models
{
    public class PayMongoWebhookModel
    {
        public PayMongoData data { get; set; }

        public class PayMongoData
        {
            public string id { get; set; }
            public string type { get; set; }
            public Attributes attributes { get; set; }
        }

        public class Attributes
        {
            public Billing billing { get; set; }
            public string status { get; set; }
            public List<LineItem> line_items { get; set; }
            public int amount { get; set; }
        }

        public class Billing
        {
            public string email { get; set; }
            public string name { get; set; }
        }

        public class LineItem
        {
            public int quantity { get; set; }
            public string description { get; set; }
        }
    }

}
