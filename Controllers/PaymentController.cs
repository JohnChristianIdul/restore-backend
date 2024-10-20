using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using Restore_backend_deployment_.Models;
using RestSharp;
using ReStore___backend.Services.Interfaces;
using System.Net.Mail;
using System.Net;
using Org.BouncyCastle.Asn1.Cms;
using Newtonsoft.Json.Linq;


namespace Restore_backend_deployment_.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly string _payMongoApiKey;
        private const int PricePerCredit = 50;
        private readonly IDataService _dataService;
        private readonly string _smtpEmail;
        private readonly string _smtpPassword;

        public PaymentController(IDataService dataService)
        {
            _payMongoApiKey = Environment.GetEnvironmentVariable("PAYMONGO_SECRET_KEY") ?? throw new ArgumentNullException("PayMongo API Key is not set in environment variables."); ;
            _smtpPassword = Environment.GetEnvironmentVariable("SMTP_EMAIL_PASSWORD");
            _smtpEmail = Environment.GetEnvironmentVariable("SMTP_EMAIL");
            _dataService = dataService;
            Console.WriteLine($"PayMongoApiKey : {_payMongoApiKey}");
        }

        // POST api/payment/buy-credits
        [HttpPost("buy-credits")]
        public async Task<IActionResult> CreateCheckoutSession(
            [FromForm] string name,
            [FromForm] string email,
            [FromForm] string phone,
            [FromForm] int creditsToPurchase)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) ||
                creditsToPurchase <= 0)
            {
                return BadRequest(new { message = "Invalid input data." });
            }

            int pricePerCredit = 25;
            int totalAmount = creditsToPurchase * pricePerCredit * 10;

            var checkoutSessionBody = new
            {
                data = new
                {
                    attributes = new
                    {
                        billing = new
                        {
                            name = name,
                            email = email,
                            phone = phone
                        },
                        send_email_receipt = true,
                        show_description = true,
                        show_line_items = true,
                        billing_information_fields_editable = "disabled",
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

            // Create the checkout session in PayMongo and return the checkout URL.
            var checkoutSessionResponse = await CreateCheckoutSessionInPayMongo(checkoutSessionBody);

            if (checkoutSessionResponse == null)
            {
                return BadRequest(new { message = "Failed to create checkout session." });
            }

            // Return the checkout URL to the client for the payment to be made
            string checkoutUrl = checkoutSessionResponse.data.attributes.checkout_url.ToString();
            string checkoutSessionId = checkoutSessionResponse.data.id.ToString();

            return Ok(new
            {
                message = "Checkout session created successfully.",
                checkout_url = checkoutUrl,
                id = checkoutSessionId
            });
        }

        // api/payment/paymongo-webhook
        [HttpPost("paymongo-webhook")]
        public async Task<IActionResult> HandlePaymentWebhook([FromBody] string sessionId)
        {
            // Log the received webhook for debugging purposes
            Console.WriteLine("Received PayMongo Webhook for session ID: " + sessionId);

            try
            {
                // Fetch the checkout session details from PayMongo
                var checkoutSessionDetails = await GetCheckoutSessionDetails(sessionId);

                if (checkoutSessionDetails == null)
                {
                    return BadRequest(new { message = "Failed to retrieve checkout session details." });
                }

                // Extract relevant data from the checkout session details
                var payments = checkoutSessionDetails.data.attributes.payments;

                if (payments.Count == 0)
                {
                    return BadRequest(new { message = "No payment information found" });
                }

                var paymentStatus = payments[0]["attributes"]["status"].ToString();
                var email = checkoutSessionDetails.data.attributes.billing.email;

                if (paymentStatus == "paid")
                {
                    var lineItems = checkoutSessionDetails.data.attributes.line_items;

                    if (!lineItems.HasValues)
                    {
                        return BadRequest(new { message = "No line items found in the checkout session." });
                    }

                    int quantity = lineItems[0]["quantity"]; 

                    Console.WriteLine($"Quantity value is {quantity}.");
                    await _dataService.SaveCustomerCreditsAsync(email.ToString(), quantity);
                    

                    var paymentReceipt = new PaymentReceipt
                    {
                        Email = email,
                        CheckoutSessionId = sessionId,
                        PaymentDate = DateTime.UtcNow,
                        Amount = checkoutSessionDetails.data.attributes.amount,
                        Description = "Buying credits for Restore",
                        Quantity = quantity
                    };

                    await _dataService.SavePaymentReceiptAsync(paymentReceipt);
                    await SendEmailReceiptAsync(email, paymentReceipt);
                    await ExpireCheckoutSession(sessionId);

                    return Ok(new { message = "Payment processed successfully." });
                }

                return BadRequest(new { message = "Payment not successful or status unknown." });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing webhook: " + ex.Message);
                return StatusCode(500, "Internal server error while processing payment.");
            }
        }

        private async Task<dynamic> GetCheckoutSessionDetails(string checkoutSessionId)
        {
            try
            {
                var options = new RestClientOptions($"https://api.paymongo.com/v1/checkout_sessions/{checkoutSessionId}")
                {
                    ThrowOnAnyError = true,
                    Timeout = TimeSpan.FromMilliseconds(20000)
                };

                var client = new RestClient(options);
                var request = new RestRequest();

                // Add headers
                request.AddHeader("accept", "application/json");
                request.AddHeader("authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(_payMongoApiKey))}");

                // Execute the GET request
                var response = await client.GetAsync(request);

                // Log the full response content for debugging
                Console.WriteLine("Response Status: " + response.StatusCode);
                Console.WriteLine("Response Content: " + response.Content);

                if (response.IsSuccessful)
                {
                    return JsonConvert.DeserializeObject<dynamic>(response.Content);
                }
                else
                {
                    // Log the failure details
                    Console.WriteLine($"Error: {response.StatusCode} - {response.Content}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                throw;
            }
        }

        // api/payment/customer-credits
        [HttpGet("customer-credits")]
        public async Task<IActionResult> GetCustomerCredits([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest("Email cannot be empty.");
            }

            try
            {
                int credits = await _dataService.GetCustomerCreditsAsync(email);
                return Ok(new { Email = email, CreditsRemaining = credits });
            }
            catch (Exception ex)
            {
                // Log the error and return a server error response
                Console.WriteLine($"Error retrieving customer credits: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        public async Task<dynamic> CreateCheckoutSessionInPayMongo(object checkoutSessionBody)
        {
            try
            {
                var options = new RestClientOptions("https://api.paymongo.com/v1/checkout_sessions")
                {
                    ThrowOnAnyError = true,
                    Timeout = TimeSpan.FromMilliseconds(10000)
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

                if (response.IsSuccessful)
                {
                    // Deserialize and return the response content
                    return JsonConvert.DeserializeObject<dynamic>(response.Content);
                }
                else
                {
                    // Log the failure details
                    Console.WriteLine($"Error: {response.StatusCode} - {response.Content}");
                    throw new Exception($"Error: {response.StatusCode} - {response.Content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
                }

                throw;
            }
        }

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
                    IsBodyHtml = true,
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