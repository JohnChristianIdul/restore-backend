using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Services.Interfaces;
using System.Globalization;

namespace ReStore___backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly IDataService _dataService;

        public SalesController(IDataService dataService)
        {
            _dataService = dataService;
        }

        [HttpPost("upload/sales")]
        public async Task<IActionResult> UploadSalesFile(IFormFile file, [FromForm] string username, [FromForm] string email)
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
                    Console.WriteLine($"{email}");
                    await _dataService.ProcessAndUploadDataSales(records, username, email);

                    return Ok(new { success = "Data processed and uploaded to Cloud Storage" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        // GET method to retrieve sales data for a specific user
        [HttpGet("sales/{email}")]
        public async Task<IActionResult> GetSalesData(string username)
        {
            if (string.IsNullOrEmpty(username))
                return BadRequest(new { error = "Username is required." });

            try
            {
                // Call the service to get sales data
                var salesDataJson = await _dataService.GetSalesDataFromStorageByUsername(username);
                return Ok(new { data = salesDataJson });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }
    }
}
