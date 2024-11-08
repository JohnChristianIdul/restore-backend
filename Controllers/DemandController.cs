using Microsoft.AspNetCore.Http;
using CsvHelper;
using ReStore___backend.Services.Interfaces;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using ExcelDataReader;

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
                var extension = Path.GetExtension(file.FileName).ToLower();
                List<dynamic> records = new List<dynamic>();

                // Create a temporary file path to store the uploaded file
                var filePath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Process the file based on the extension (Excel or CSV)
                if (extension == ".xlsx" || extension == ".xls")
                {
                    // Process Excel file
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (var excelReader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var dataSet = excelReader.AsDataSet(new ExcelDataSetConfiguration
                        {
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration
                            {
                                UseHeaderRow = true
                            }
                        });

                        var dataTable = dataSet.Tables[0];

                        foreach (System.Data.DataRow row in dataTable.Rows)
                        {
                            DateTime monthDate;
                            string monthFormatted = DateTime.TryParse(row["Month"].ToString(), out monthDate) ? monthDate.ToString("MM/dd/yyyy") : row["Month"].ToString();

                            records.Add(new
                            {
                                Month = monthFormatted,
                                ProductID = row["ProductID"].ToString(),
                                Product = row["Product"].ToString(),
                                UnitsSold = row["UnitsSold"].ToString()
                            });
                        }
                    }
                }
                else if (extension == ".csv")
                {
                    // Process CSV file
                    using (var reader = new StreamReader(filePath))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        records = csv.GetRecords<dynamic>().ToList();
                    }
                }
                else
                {
                    return BadRequest(new { error = "Unsupported file format. Please upload a CSV or Excel file." });
                }

                // Call the service to process and upload the data
                await _dataService.ProcessAndUploadDataDemands(records, username, email);

                return Ok(new { success = "Data processed and uploaded to Cloud Storage", records_data = records });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"An error occurred while processing the file: {ex.Message}" });
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
