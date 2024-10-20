using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using Restore_backend_deployment_.Models;
using RestSharp;
using ReStore___backend.Services.Interfaces;
using System.Net.Mail;
using System.Net;


namespace Restore_backend_deployment_.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _payMongoApiKey;
        private const int PricePerCredit = 50;
        private readonly string _payMongoBaseUrl = "https://api.paymongo.com/v1/checkout_sessions";
        private readonly IDataService _dataService;


        PaymentController(IConfiguration configuration, IDataService dataService)
        {
            _configuration = configuration;            
            _payMongoApiKey = Environment.GetEnvironmentVariable("PAYMONGO_SECRET_KEY");
            _dataService = dataService;
        }

        // POST api/payment/buy-credits
        [HttpPost("buy-credits")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutSessionRequest request)
        {
            string email = request.Data.Attributes.Billing.Email;
            int creditsToPurchase = request.Credits;

            bool paymentSuccess = true;
            string checkoutSessionId = "your_checkout_session_id";

            if (paymentSuccess)
            {
                // Save customer credits
                await _dataService.SaveCustomerCreditsAsync(email, creditsToPurchase);

                // Save payment receipt
                var paymentReceipt = new PaymentReceipt
                {
                    Email = email,
                    CheckoutSessionId = checkoutSessionId,
                    PaymentDate = DateTime.UtcNow,
                    Amount = creditsToPurchase * PricePerCredit * 100,
                    Description = "Buying credits for Restore"
                };
                await _dataService.SavePaymentReceiptAsync(paymentReceipt);
                await SendEmailReceiptAsync(email, paymentReceipt);

                // Expire the checkout session after successful payment
                await ExpireCheckoutSession(checkoutSessionId);

                return Ok(new { message = "Payment successful and data saved." });
            }

            return BadRequest(new { message = "Payment failed." });
        }

        private async Task<CheckoutSessionRequest> CreateCheckoutSessionInPayMongo(CheckoutSessionRequest request)
        {
            var options = new RestClientOptions(_payMongoBaseUrl)
            {
                ThrowOnAnyError = true,
                Timeout = TimeSpan.FromMilliseconds(3000)
            };

            var client = new RestClient(options);
            var restRequest = new RestRequest();
            restRequest.AddHeader("accept", "application/json");
            restRequest.AddHeader("authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(_payMongoApiKey))}");
            restRequest.AddJsonBody(request); // Add the checkout session request body

            var response = await client.PostAsync(restRequest);
            if (response.IsSuccessful)
            {
                return JsonConvert.DeserializeObject<CheckoutSessionRequest>(response.Content);
            }

            // Log the error or handle it as needed
            throw new Exception("Failed to create checkout session: " + response.Content);
        }


        // Method to expire the checkout session
        private async Task ExpireCheckoutSession(string sessionId)
        {
            var options = new RestClientOptions($"https://api.paymongo.com/v1/checkout_sessions/{sessionId}/expire")
            {
                ThrowOnAnyError = true,
                Timeout = TimeSpan.FromMilliseconds(3000)
            };

            var client = new RestClient(options);
            var request = new RestRequest();
            request.AddHeader("accept", "application/json");
            request.AddHeader("authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(_payMongoApiKey))}");

            var response = await client.PostAsync(request);
            if (!response.IsSuccessful)
            {
                // Log the error or handle it as needed
                throw new Exception("Failed to expire checkout session: " + response.Content);
            }
        }
        private async Task SendEmailReceiptAsync(string email, PaymentReceipt receipt)
        {
            var subject = "Your Payment Receipt";
            var body = BuildEmailBody(receipt);

            using (var smtpClient = new SmtpClient("smtp.your-email-provider.com") // Update with your SMTP server
            {
                Port = 587, // or your SMTP port
                Credentials = new NetworkCredential("your-email@example.com", "your-email-password"), // Update with your credentials
                EnableSsl = true,
            })
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress("your-email@example.com"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true, // Set to true if you're using HTML for body
                };

                mailMessage.To.Add(email);

                await smtpClient.SendMailAsync(mailMessage);
            }
        }
        private string BuildEmailBody(PaymentReceipt receipt)
        {
            var sb = new StringBuilder();
            sb.Append("<div style='border: 1px solid #000; padding: 10px; width: 300px;'>");
            sb.Append("<h2 style='text-align: center;'>Payment Receipt</h2>");
            sb.Append("<p><strong>Email:</strong> " + receipt.Email + "</p>");
            sb.Append("<p><strong>Checkout Session ID:</strong> " + receipt.CheckoutSessionId + "</p>");
            sb.Append("<p><strong>Payment Date:</strong> " + receipt.PaymentDate.ToString("g") + "</p>");
            sb.Append("<p><strong>Amount:</strong> " + (receipt.Amount / 100m).ToString("C") + "</p>"); // Assuming amount is in cents
            sb.Append("<p><strong>Description:</strong> " + receipt.Description + "</p>");
            sb.Append("</div>");

            return sb.ToString();
        }
    }
}