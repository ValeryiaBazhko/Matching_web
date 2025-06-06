// Controllers/HomeController.cs
using Microsoft.AspNetCore.Mvc;
using MatchingApp.Models;
using MatchingApp.Services;
using System.Diagnostics;
using Microsoft.AspNetCore.Http.Timeouts;

namespace MatchingApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly Services.MatchingService _matchingService;

        public HomeController(Services.MatchingService matchingService)
        {
            _matchingService = matchingService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult StartSession()
        {
            var sessionId = Guid.NewGuid();
            return RedirectToAction("Match", new { sessionId });
        }

        public async Task<IActionResult> Match(Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                return RedirectToAction("Index");
            }

            var pair = await _matchingService.GetNextPairAsync(sessionId);
            
            if (pair == null)
            {
                return RedirectToAction("Complete", new { sessionId });
            }

            return View(pair);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitEvaluation(Guid sessionId, int desc1Id, int desc2Id, bool isMatch)
        {
            await _matchingService.SaveEvaluationAsync(sessionId, desc1Id, desc2Id, isMatch);
            return RedirectToAction("Match", new { sessionId });
        }

        public async Task<IActionResult> Complete(Guid sessionId)
        {
            var progress = await _matchingService.GetSessionProgressAsync(sessionId);
            ViewBag.CompletedPairs = progress;
            return View();
        }

        [HttpGet]
        public IActionResult ImportData()
        {
            return View();
        }

        [HttpPost]
        [RequestSizeLimit(200_000_000)] // 200MB limit
        [RequestTimeout(300000)] // 5 minutes timeout
        public async Task<IActionResult> ImportData(IFormFile csvFile, int columnNumber = 22, int maxRecords = 0)
        {
            // Debug information
            ViewBag.ColumnNumber = columnNumber;
            ViewBag.MaxRecords = maxRecords;
            
            // Check if any file was uploaded
            if (Request.Form.Files.Count == 0)
            {
                ViewBag.Error = "No file was uploaded. Please select a CSV file.";
                return View();
            }
            
            if (csvFile == null)
            {
                ViewBag.Error = "File upload failed. Please try again with a smaller file or check your internet connection.";
                return View();
            }
            
            if (csvFile.Length == 0)
            {
                ViewBag.Error = "The uploaded file is empty. Please select a valid CSV file.";
                return View();
            }

            if (csvFile.Length > 200_000_000) // 200MB
            {
                ViewBag.Error = "File too large. Maximum size is 200MB. Your file is " + (csvFile.Length / 1_000_000) + "MB.";
                return View();
            }

            // Check file extension
            var fileExtension = Path.GetExtension(csvFile.FileName).ToLowerInvariant();
            if (fileExtension != ".csv")
            {
                ViewBag.Error = $"Invalid file type '{fileExtension}'. Please upload a .csv file.";
                return View();
            }

            try
            {
                ViewBag.Processing = true;
                
                // Process file in chunks to handle large files
                var columnIndex = columnNumber - 1;
                var importedCount = await _matchingService.ImportDescriptionsFromCsvStreamAsync(csvFile.OpenReadStream(), columnIndex, maxRecords);
                
                var message = $"Successfully imported {importedCount} descriptions from column {columnNumber}!";
                if (maxRecords > 0)
                {
                    message += $" (Limited to first {maxRecords} records)";
                }
                
                ViewBag.Success = message;
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error importing data: {ex.Message}";
                return View();
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}