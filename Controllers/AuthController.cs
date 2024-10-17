using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Dtos;
using ReStore___backend.Services.Interfaces;
using Restore_backend_deployment_.DTO_s;
using System.Net;
using System.Net.Http;
using System.Net.Mail;

namespace ReStore___backend.Controllers
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IDataService _dataService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IDataService dataService, HttpClient httpClient, ILogger<AuthController> logger)
        {
            _dataService = dataService;
            _httpClient = httpClient;
            _logger = logger;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] SignUpDTO signUpDto)
        {
            if (signUpDto == null)
                return BadRequest(new { error = "Invalid sign-up data." });

            try
            {
                // Call service method for sign-up
                var result = await _dataService.SignUp(
                    signUpDto.Email,
                    signUpDto.Name,
                    signUpDto.Username,
                    signUpDto.PhoneNumber,
                    signUpDto.Password                                 
                );

                if (result.StartsWith("Error"))
                    return BadRequest(new { error = result });

                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO loginDto)
        {
            if (loginDto == null)
                return BadRequest(new { error = "Invalid login data." });

            try
            {
                // Call service method for login, which now returns a LoginResultDTO
                var loginResult = await _dataService.Login(loginDto.Email, loginDto.Password);

                // Check if there was an error during login
                if (loginResult.Token.StartsWith("Error"))
                    return Unauthorized(new { error = loginResult.Token });

                // Return the token and username from the LoginResultDTO
                return Ok(new { token = loginResult.Token, username = loginResult.Username });
            }
            catch (Exception ex)
            {
                // Return an internal server error with the exception message
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        [HttpPost("sendPasswordResetEmail")]
        public async Task<IActionResult> SendPasswordResetEmail([FromBody] string email)
        {
            try
            {
                await _dataService.SendPasswordResetEmailAsync(email);
                return Ok("Password reset email sent.");
            }
            catch (FirebaseAuthException ex)
            {
                return BadRequest($"Error: {ex.Message}");
            }
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail(string oobCode)
        {
            _logger.LogInformation($"Received oobCode: {oobCode}");

            if (string.IsNullOrEmpty(oobCode))
            {
                _logger.LogError("No oobCode received");
                return BadRequest("Verification code is missing.");
            }

            try
            {
                // Call to Firebase to verify the email using the oobCode without exposing the API key in the URL.
                var firebaseResponse = await _httpClient.PostAsync(
                    "https://identitytoolkit.googleapis.com/v1/accounts:update",
                    JsonContent.Create(new { oobCode })  // Only passing the oobCode here
                );

                if (!firebaseResponse.IsSuccessStatusCode)
                {
                    var errorContent = await firebaseResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Firebase error: {errorContent}");
                    return BadRequest($"Verification failed: {errorContent}");
                }

                // If successful, handle Firestore user update (if needed)
                string verificationResult = await _dataService.VerifyEmail(oobCode);

                _logger.LogInformation($"Verification result: {verificationResult}");

                if (!string.IsNullOrEmpty(verificationResult) && verificationResult.StartsWith("Error"))
                {
                    return BadRequest(verificationResult);
                }

                return Ok("Email verified successfully! You can now log in.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during verification: {ex.Message}");
                return StatusCode(500, "An error occurred during verification.");
            }
        }

    }
}
