using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using ReStore___backend.Services.Interfaces;
using System.Globalization;
using System.IO;

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

            // Declare the file paths outside the try block
            string tempExcelFilePath = null;
            string csvFilePath = null;

            try
            {
                List<dynamic> records;
                var fileExtension = Path.GetExtension(file.FileName).ToLower();

                if (fileExtension == ".xlsx" || fileExtension == ".xls")
                {
                    // Handle Excel file
                    tempExcelFilePath = Path.Combine(Path.GetTempPath(), file.FileName);
                    using (var stream = new FileStream(tempExcelFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    csvFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(file.FileName) + ".csv");
                    using (var package = new ExcelPackage(new FileInfo(tempExcelFilePath)))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension.Rows;
                        var colCount = worksheet.Dimension.Columns;

                        using (var writer = new StreamWriter(csvFilePath))
                        {
                            // Write the headers
                            for (int col = 1; col <= colCount; col++)
                            {
                                writer.Write(worksheet.Cells[1, col].Text);
                                if (col < colCount) writer.Write(",");
                            }
                            writer.WriteLine();

                            for (int row = 2; row <= rowCount; row++)
                            {
                                for (int col = 1; col <= colCount; col++)
                                {
                                    writer.Write(worksheet.Cells[row, col].Text);
                                    if (col < colCount) writer.Write(",");
                                }
                                writer.WriteLine();
                            }
                        }
                    }

                    // Read from the generated CSV file
                    using (var reader = new StreamReader(csvFilePath))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        records = csv.GetRecords<dynamic>().ToList();
                    }
                }
                else if (fileExtension == ".csv")
                {
                    // Handle CSV file directly
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        records = csv.GetRecords<dynamic>().ToList();
                    }
                }
                else
                {
                    return BadRequest(new { error = "Unsupported file format. Please upload an Excel or CSV file." });
                }

                await _dataService.ProcessAndUploadDataSales(records, username, email);

                return Ok(new { success = "Data processed and uploaded to Cloud Storage" });
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes (optional)
                Console.WriteLine($"Error: {ex.Message}");

                // Return a 500 status code with the error message
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
            }
            finally
            {
                // Clean up temporary files, check if file paths are not null
                if (!string.IsNullOrEmpty(tempExcelFilePath) && System.IO.File.Exists(tempExcelFilePath))
                {
                    System.IO.File.Delete(tempExcelFilePath);
                }
                if (!string.IsNullOrEmpty(csvFilePath) && System.IO.File.Exists(csvFilePath))
                {
                    System.IO.File.Delete(csvFilePath);
                }
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
