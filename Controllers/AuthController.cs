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

                // Check if there was an error during login using ErrorMessage
                if (!string.IsNullOrEmpty(loginResult.ErrorMessage))
                    return Unauthorized(new { error = loginResult.ErrorMessage });

                // Return the token and username from the LoginResultDTO
                return Ok(new { token = loginResult.Token, username = loginResult.Username });
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                Console.WriteLine($"Unexpected error during login: {ex}");

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

            // Validate the input
            if (string.IsNullOrWhiteSpace(oobCode))
            {
                _logger.LogError("No oobCode received");
                return BadRequest(new { error = "Verification code is missing." });
            }

            try
            {
                // Send the oobCode to Firebase for verification
                var (success, message) = await _dataService.VerifyEmail(oobCode);

                // If verification was unsuccessful, return the error
                if (!success)
                {
                    _logger.LogError($"Firebase verification failed: {message}");
                    return BadRequest(new { error = $"Verification failed: {message}" });
                }

                // Optionally update the user record in Firestore
                // Here, you would call a method to update the user's verification status in Firestore
                // Assuming you have a method like `UpdateUserVerificationStatus`
                var firestoreUpdateResult = await _dataService.UpdateUserVerificationStatus(oobCode);

                if (!string.IsNullOrEmpty(firestoreUpdateResult) && firestoreUpdateResult.StartsWith("Error"))
                {
                    return BadRequest(new { error = firestoreUpdateResult });
                }

                // Successful verification response
                return Ok(new { message = "Email verified successfully! You can now log in." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception during email verification: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred during verification." });
            }
        }
    }
}
