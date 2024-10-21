using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Services.Interfaces;

namespace ReStore___backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesPredictionController : ControllerBase
    {
        private readonly IDataService _dataService;

        public SalesPredictionController(IDataService dataService)
        {
            _dataService = dataService;
        }

        [HttpGet("prediction")]
        public async Task<IActionResult> GetDemandPrediction([FromQuery] string username)
        {
            var result = await _dataService.GetSalesPrediction(username);
            return Ok(result);
        }
    }
}
