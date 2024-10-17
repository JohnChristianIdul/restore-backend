using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Dtos;
using ReStore___backend.Services.Interfaces;
using Restore_backend_deployment_.DTO_s;
using System.Net;
using System.Net.Mail;

namespace ReStore___backend.Controllers
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IDataService _dataService;

        public AuthController(IDataService dataService)
        {
            _dataService = dataService;
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

        [HttpPost("verifyEmail")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDTO verifyEmailDto)
        {
            if (verifyEmailDto == null || string.IsNullOrWhiteSpace(verifyEmailDto.UserId) || string.IsNullOrWhiteSpace(verifyEmailDto.Token))
                return BadRequest(new { error = "Invalid verification data." });
            try
            {
                var result = await _dataService.VerifyEmail(verifyEmailDto.Token);
                if (result.StartsWith("Error"))
                    return BadRequest(new { error = result });
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}
