using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
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
                var filePath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                List<dynamic> records;

                if (file.ContentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
                    file.ContentType == "application/vnd.ms-excel")
                {
                    // Handle Excel file
                    using (var package = new ExcelPackage(new FileInfo(filePath)))
                    {
                        var worksheet = package.Workbook.Worksheets[0]; // Get the first worksheet
                        var rowCount = worksheet.Dimension.Rows;
                        var colCount = worksheet.Dimension.Columns;

                        records = new List<dynamic>();

                        var headers = new List<string>();
                        for (int col = 1; col <= colCount; col++)
                        {
                            headers.Add(worksheet.Cells[1, col].Text);
                        }

                        for (int row = 2; row <= rowCount; row++) // Start from the second row
                        {
                            var record = new Dictionary<string, string>();
                            for (int col = 1; col <= colCount; col++)
                            {
                                record[headers[col - 1]] = worksheet.Cells[row, col].Text;
                            }
                            records.Add(record);
                        }
                    }
                }
                else if (file.ContentType == "text/csv")
                {
                    // Handle CSV file
                    using (var reader = new StreamReader(filePath))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        records = csv.GetRecords<dynamic>().ToList();
                    }
                }
                else
                {
                    return BadRequest(new { error = "Unsupported file type." });
                }

                // Call the service to process and upload the data
                Console.WriteLine($"{email}");
                await _dataService.ProcessAndUploadDataSales(records, username, email);

                return Ok(new { success = "Data processed and uploaded to Cloud Storage" });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
        }

        // GET method to retrieve sales data for a specific user
        // api/Sales/sales?username={username}
        [HttpGet("sales/")]
        public async Task<IActionResult> GetSalesData([FromQuery] string username)
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
