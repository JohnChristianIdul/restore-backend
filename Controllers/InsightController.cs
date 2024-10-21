﻿using CsvHelper;
using ExcelDataReader;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Services.Interfaces;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace ReStore___backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InsightController : ControllerBase
    {
        private readonly IDataService _dataService;

        // Constructor injection of IDataService
        public InsightController(IDataService dataService)
        {
            _dataService = dataService;
        }

        // GET: api/insights/{email}
        [HttpGet("{email}")]
        public async Task<IActionResult> GetInsights(string email)
        {
            var insightJson = await _dataService.GetSalesInsightByEmail(email);

            // Return the JSON file of the insight
            return Content(insightJson, "application/json");
        }
    }
}