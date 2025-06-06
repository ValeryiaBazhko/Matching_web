// Services/MatchingService.cs
using Microsoft.EntityFrameworkCore;
using MatchingApp.Data;
using MatchingApp.Models;

namespace MatchingApp.Services
{
    public class MatchingService
    {
        private readonly ApplicationDbContext _context;
        private const int TOTAL_PAIRS_PER_SESSION = 50;

        public MatchingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MatchingPair?> GetNextPairAsync(Guid sessionId)
        {
            // Get how many evaluations this session has already done
            var completedCount = await _context.Evaluations
                .CountAsync(e => e.SessionId == sessionId);

            if (completedCount >= TOTAL_PAIRS_PER_SESSION)
            {
                return null; // Session completed
            }

            // Get all descriptions
            var descriptions = await _context.Descriptions.ToListAsync();
            
            if (descriptions.Count < 2)
            {
                return null; // Not enough descriptions
            }

            // Get pairs already evaluated by this session
            var evaluatedPairs = await _context.Evaluations
                .Where(e => e.SessionId == sessionId)
                .Select(e => new { e.Description1Id, e.Description2Id })
                .ToListAsync();

            // Find a pair that hasn't been evaluated yet
            var random = new Random();
            Description? desc1 = null, desc2 = null;
            int attempts = 0;
            
            while (attempts < 100) // Prevent infinite loop
            {
                desc1 = descriptions[random.Next(descriptions.Count)];
                desc2 = descriptions[random.Next(descriptions.Count)];
                
                if (desc1.Id != desc2.Id && 
                    !evaluatedPairs.Any(p => 
                        (p.Description1Id == desc1.Id && p.Description2Id == desc2.Id) ||
                        (p.Description1Id == desc2.Id && p.Description2Id == desc1.Id)))
                {
                    break;
                }
                attempts++;
            }

            if (desc1 == null || desc2 == null || desc1.Id == desc2.Id)
            {
                return null; // Couldn't find a unique pair
            }

            return new MatchingPair
            {
                Description1 = desc1,
                Description2 = desc2,
                CurrentPairIndex = completedCount + 1,
                TotalPairs = TOTAL_PAIRS_PER_SESSION,
                SessionId = sessionId
            };
        }

        public async Task SaveEvaluationAsync(Guid sessionId, int desc1Id, int desc2Id, bool isMatch)
        {
            var evaluation = new Evaluation
            {
                SessionId = sessionId,
                Description1Id = desc1Id,
                Description2Id = desc2Id,
                IsMatch = isMatch,
                CreatedAt = DateTime.UtcNow
            };

            _context.Evaluations.Add(evaluation);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetSessionProgressAsync(Guid sessionId)
        {
            return await _context.Evaluations
                .CountAsync(e => e.SessionId == sessionId);
        }

        public async Task<int> ImportDescriptionsFromCsvStreamAsync(Stream csvStream, int columnIndex = 22, int maxRecords = 0)
        {
            try
            {
                var descriptions = new List<Description>();
                var importedCount = 0;
                var processedLines = 0;
                const int batchSize = 1000; // Process in batches to avoid memory issues
                
                using var reader = new StreamReader(csvStream);
                
                // Skip header line
                await reader.ReadLineAsync();
                
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    // Stop if we've reached the maximum number of records
                    if (maxRecords > 0 && importedCount >= maxRecords)
                    {
                        break;
                    }
                    
                    processedLines++;
                    
                    try
                    {
                        var columns = ParseCsvLine(line);
                        
                        if (columns.Count > columnIndex)
                        {
                            var content = columns[columnIndex].Trim().Trim('"');
                            if (!string.IsNullOrWhiteSpace(content) && content.Length > 10) // Only meaningful descriptions
                            {
                                descriptions.Add(new Description { Content = content });
                                importedCount++;
                                
                                // Save in batches to avoid memory issues
                                if (descriptions.Count >= batchSize)
                                {
                                    if (importedCount <= batchSize) // First batch, clear existing data
                                    {
                                        _context.Descriptions.RemoveRange(_context.Descriptions);
                                    }
                                    
                                    _context.Descriptions.AddRange(descriptions);
                                    await _context.SaveChangesAsync();
                                    descriptions.Clear();
                                    
                                    // Log progress
                                    Console.WriteLine($"Imported {importedCount} descriptions so far...");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log and continue with next line
                        Console.WriteLine($"Error parsing line {processedLines}: {ex.Message}");
                        continue;
                    }
                }
                
                // Save remaining descriptions
                if (descriptions.Any())
                {
                    if (importedCount <= batchSize) // Only one batch, clear existing data
                    {
                        _context.Descriptions.RemoveRange(_context.Descriptions);
                    }
                    
                    _context.Descriptions.AddRange(descriptions);
                    await _context.SaveChangesAsync();
                }
                
                if (importedCount == 0)
                {
                    throw new Exception($"No valid descriptions found in column {columnIndex + 1}. Make sure the column contains text longer than 10 characters.");
                }
                
                Console.WriteLine($"Import completed: {importedCount} descriptions imported from {processedLines} processed lines");
                return importedCount;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error importing CSV: {ex.Message}");
            }
        }

        public async Task ImportDescriptionsFromCsvAsync(string csvContent, int columnIndex = 22)
        {
            try
            {
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length == 0)
                {
                    throw new Exception("CSV file is empty");
                }

                var descriptions = new List<Description>();
                
                // Skip header row (first line)
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    // Split by comma, handling quoted values
                    var columns = ParseCsvLine(line);
                    
                    // Check if we have enough columns
                    if (columns.Count > columnIndex)
                    {
                        var content = columns[columnIndex].Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            descriptions.Add(new Description { Content = content });
                        }
                    }
                }

                if (descriptions.Count == 0)
                {
                    throw new Exception($"No valid descriptions found in column {columnIndex + 1}");
                }

                // Clear existing descriptions (optional - remove if you want to keep adding)
                _context.Descriptions.RemoveRange(_context.Descriptions);
                
                _context.Descriptions.AddRange(descriptions);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error importing CSV: {ex.Message}");
            }
        }

        private List<string> ParseCsvLine(string line)
        {
            var columns = new List<string>();
            var inQuotes = false;
            var currentColumn = "";
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    columns.Add(currentColumn);
                    currentColumn = "";
                }
                else
                {
                    currentColumn += c;
                }
            }
            
            // Add the last column
            columns.Add(currentColumn);
            
            return columns;
        }
    }
}