using Microsoft.AspNetCore.Http;
using CsvHelper;
using ReStore___backend.Services.Interfaces;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace ReStore___backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemandController : ControllerBase
    {
        private readonly IDataService _dataService;

        public DemandController(IDataService dataService)
        {
            _dataService = dataService;
        }

        [HttpPost("upload/demand")]
        public async Task<IActionResult> UploadDemandFile(IFormFile file, [FromForm] string username, [FromForm] string email)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            if (string.IsNullOrEmpty(username))
                return BadRequest(new { error = "Username is required." });
            
            if (string.IsNullOrEmpty(email))
                return BadRequest(new { error = "Email is required." });

            try
            {
                // Process CSV file
                var filePath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<dynamic>().ToList();

                    // Call the service to process and upload the data
                    Console.WriteLine($"{username} {email}");
                    await _dataService.ProcessAndUploadDataDemands(records, username, email);
                    Console.WriteLine("Error occured here");

                    return Ok(new { success = "Data processed and uploaded to Cloud Storage" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Entered an Exception : {ex.GetType}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        // api/Demands/demand?username={username}
        [HttpGet("demand/")]
        public async Task<IActionResult> GetDemandData([FromQuery] string username)
        {
            if (string.IsNullOrEmpty(username))
                return BadRequest(new { error = "Username is required." });
            try
            {
                string demandDataJson = await _dataService.GetDemandDataFromStorageByUsername(username);
                return Ok(demandDataJson);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}
