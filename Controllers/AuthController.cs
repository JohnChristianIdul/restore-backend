using Firebase.Auth.Objects;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Identity.Data;
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
        public async Task<IActionResult> Login([FromBody] LoginDTO login)
        {
            if (string.IsNullOrWhiteSpace(login.Email) || string.IsNullOrWhiteSpace(login.Password))
            {
                return BadRequest(new { error = "Email and password are required." });
            }

            try
            {
                var loginResult = await _dataService.Login(login.Email, login.Password);

                // Check if there was an error (like unverified email)
                if (!string.IsNullOrEmpty(loginResult.ErrorMessage))
                {
                    // Return the error message from LoginResultDTO, including "Email is not verified"
                    return BadRequest(new { error = loginResult.ErrorMessage });
                }

                // Successful login response with token and username
                return Ok(new
                {
                    message = "Login successful!",
                    token = loginResult.Token,
                    username = loginResult.Username
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during login: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred during login." });
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

            // Validate the input
            if (string.IsNullOrWhiteSpace(oobCode))
            {
                _logger.LogError("No oobCode received");
                return BadRequest(new { error = "Verification code is missing." });
            }

            try
            {
                // Send the oobCode to Firebase for verification
                var verificationResult = await _dataService.VerifyEmail(oobCode);

                // If verification was unsuccessful, return the error
                if (!verificationResult.success)
                {
                    _logger.LogError($"Firebase verification failed: {verificationResult.message}");
                    return BadRequest(new { error = $"Verification failed: {verificationResult.message}" });
                }

                // Successful verification response
                return Ok(new { message = "Your email has been verified. You can now sign in with your new account." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during email verification: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred during verification." });
            }
        }
    }
}
