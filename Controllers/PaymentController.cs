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
        private readonly string _smtpEmail;
        private readonly string _smtpPassword;


        PaymentController(IConfiguration configuration, IDataService dataService)
        {
            _configuration = configuration;            
            _payMongoApiKey = Environment.GetEnvironmentVariable("PAYMONGO_SECRET_KEY");
            _smtpPassword = Environment.GetEnvironmentVariable("SMTP_EMAIL_PASSWORD");
            _smtpEmail = Environment.GetEnvironmentVariable("SMTP_EMAIL");
            _dataService = dataService;
            Console.WriteLine($"PayMongoApiKey : {_payMongoApiKey}");
        }

        // POST api/payment/buy-credits
        [HttpPost("buy-credits")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CheckoutSessionRequest request)
        {
            string email = request.Data.Attributes.Billing.Email;
            int creditsToPurchase = request.Credits;

            int totalAmount = creditsToPurchase * PricePerCredit * 100;

            // Create the checkout session request body
            var checkoutSessionBody = new
            {
                data = new
                {
                    attributes = new
                    {
                        billing = new
                        {
                            name = request.Data.Attributes.Billing.Name,
                            email = email,
                            phone = request.Data.Attributes.Billing.Phone
                        },
                        send_email_receipt = true,
                        show_description = true,
                        show_line_items = true,
                        description = "Credits for Restore forecasting service.",
                        payment_method_types = new[] { "gcash" },
                        statement_descriptor = "Restore Credits",
                        line_items = new[]
                        {
                    new
                    {
                        currency = "PHP",
                        amount = totalAmount,
                        description = "Credits for Restore forecasting service.",
                        name = "Credits",
                        quantity = creditsToPurchase
                    }
                }
                    }
                }
            };

            // Call the method to create the checkout session in PayMongo
            var checkoutSessionResponse = await CreateCheckoutSessionInPayMongo(checkoutSessionBody);

            if (checkoutSessionResponse != null)
            {
                // Save customer credits
                await _dataService.SaveCustomerCreditsAsync(email, creditsToPurchase);

                // Save payment receipt
                var paymentReceipt = new PaymentReceipt
                {
                    Email = email,
                    CheckoutSessionId = checkoutSessionResponse.id,
                    PaymentDate = DateTime.UtcNow,
                    Amount = totalAmount,
                    Description = "Buying credits for Restore"
                };

                await _dataService.SavePaymentReceiptAsync(paymentReceipt);
                await SendEmailReceiptAsync(email, paymentReceipt);

                await ExpireCheckoutSession(checkoutSessionResponse.id);

                return Ok(new { message = "Payment successful and data saved.", sessionId = checkoutSessionResponse.id });
            }

            return BadRequest(new { message = "Payment failed." });
        }

        public async Task<dynamic> CreateCheckoutSessionInPayMongo(object checkoutSessionBody)
        {
            try
            {
                // Log the request body being sent to PayMongo
                Console.WriteLine("Request Body: " + JsonConvert.SerializeObject(checkoutSessionBody));

                var options = new RestClientOptions(_payMongoBaseUrl)
                {
                    ThrowOnAnyError = true,
                    Timeout = TimeSpan.FromMilliseconds(10000) // Increase timeout to 10 seconds
                };

                var client = new RestClient(options);
                var restRequest = new RestRequest();

                // Add headers
                restRequest.AddHeader("accept", "application/json");
                restRequest.AddHeader("authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(_payMongoApiKey))}");

                // Add JSON body
                restRequest.AddJsonBody(checkoutSessionBody);

                // Execute the POST request
                var response = await client.PostAsync(restRequest);

                // Log the full response content for debugging
                Console.WriteLine("Response Status: " + response.StatusCode);
                Console.WriteLine("Response Content: " + response.Content);

                // Check if the response is successful
                if (response.IsSuccessful)
                {
                    // Log success message
                    Console.WriteLine("Checkout session created successfully!");

                    // Deserialize and return the response content
                    return JsonConvert.DeserializeObject<dynamic>(response.Content);
                }
                else
                {
                    // Log the failure details with status and content
                    Console.WriteLine("Failed to create checkout session.");
                    Console.WriteLine($"Status Code: {response.StatusCode}");
                    Console.WriteLine("Response Error Content: " + response.Content);

                    // Throw an exception with the response content for further handling
                    throw new Exception($"Error: {response.StatusCode} - {response.Content}");
                }
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Console.WriteLine("An error occurred: " + ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }

                // Optionally, you could log the stack trace for deeper debugging:
                Console.WriteLine("Stack Trace: " + ex.StackTrace);

                // Rethrow or handle the exception based on your application needs
                throw;
            }
        }

        // Method to expire the checkout session
        public async Task ExpireCheckoutSession(string sessionId)
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
        public async Task SendEmailReceiptAsync(string email, PaymentReceipt receipt)
        {
            var subject = "Your Payment Receipt";
            var body = BuildEmailBody(receipt);

            using (var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587, 
                Credentials = new NetworkCredential(_smtpEmail, _smtpPassword),
                EnableSsl = true,
            })
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true, // Set to true if you're using HTML for body
                };

                mailMessage.To.Add(email);

                await smtpClient.SendMailAsync(mailMessage);
            }
        }
        public string BuildEmailBody(PaymentReceipt receipt)
        {
            var sb = new StringBuilder();
            sb.Append("<div style='border: 1px solid #000; padding: 10px; width: 300px;'>");
            sb.Append("<h2 style='text-align: center;'>Payment Receipt</h2>");
            sb.Append("<p><strong>Email:</strong> " + receipt.Email + "</p>");
            sb.Append("<p><strong>Checkout Session ID:</strong> " + receipt.CheckoutSessionId + "</p>");
            sb.Append("<p><strong>Payment Date:</strong> " + receipt.PaymentDate.ToString("g") + "</p>");
            sb.Append("<p><strong>Amount:</strong> " + (receipt.Amount / 100m).ToString("C") + "</p>");
            sb.Append("<p><strong>Description:</strong> " + receipt.Description + "</p>");
            sb.Append("</div>");

            return sb.ToString();
        }
    }
}