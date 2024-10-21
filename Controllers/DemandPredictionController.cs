using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Services.Interfaces;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Newtonsoft.Json;
using ReStore___backend.Services.Interfaces;

namespace ReStore___backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DemandPredictionController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IDataService _dataService;

        public DemandPredictionController(HttpClient httpClient, IDataService dataService)
        {
            _httpClient = httpClient;
            _dataService = dataService;
        }

        [HttpGet("prediction/{email}")]
        public async Task<IActionResult> GetDemandPrediction(string email)
        {
            var result = await _dataService.GetDemandPrediction(email);
            if (result is string && result.StartsWith("Error"))
            {
                return BadRequest(result); // or another appropriate status code
            }
            return Ok(result);
        }
    }
}