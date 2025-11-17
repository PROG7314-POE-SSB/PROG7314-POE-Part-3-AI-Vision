using Azure.AI.Vision.ImageAnalysis;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace PantryChef.VisionApi
{
    /// <summary>
    /// Defines the top-level API response structure returned by the function.
    /// </summary>
    public class PantryAiResponse
    {
        /// <summary>
        /// Indicates whether the image processing operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// On success, contains the processed pantry item data.
        /// On failure, this will be null.
        /// </summary>
        public PantryAiData? Data { get; set; }

        /// <summary>
        /// On failure, contains a descriptive error message.
        /// On success, this will be null.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Represents the structured data for a single pantry item,
    /// transformed from the raw AI Vision analysis.
    /// </summary>
    public class PantryAiData
    {
        /// <summary>
        /// The detected or inferred name of the pantry item.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// A human-readable description of the item, often derived from the AI's caption.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The inferred category for the item (e.g., "Produce", "Dairy", "Pantry").
        /// Defaults to "Other".
        /// </summary>
        public string Category { get; set; } = "Other";

        /// <summary>
        /// Placeholder for a future-implemented estimated expiry date.
        /// </summary>
        public string? EstimatedExpiry { get; set; }

        /// <summary>
        /// Placeholder for future-implemented nutritional information.
        /// </summary>
        public object NutritionalInfo { get; set; } = new { };

        /// <summary>
        /// The confidence score (0.0 to 1.0) from the AI model
        /// associated with the identified <see cref="ItemName"/>.
        /// </summary>
        public double Confidence { get; set; }
    }

    /// <summary>
    /// The main Azure Function class responsible for processing pantry item images.
    /// It receives an image via HTTP POST, analyzes it with Azure AI Vision,
    /// and returns a structured JSON response.
    /// </summary>
    public class ProcessPantryImage
    {
        private readonly ILogger _logger;
        private readonly ImageAnalysisClient _visionClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessPantryImage"/> class
        /// using dependency injection.
        /// </summary>
        /// <param name="loggerFactory">The factory to create a logger instance.</param>
        /// <param name="visionClient">The singleton <see cref="ImageAnalysisClient"/> for Azure AI Vision.</param>
        public ProcessPantryImage(ILoggerFactory loggerFactory, ImageAnalysisClient visionClient)
        {
            _logger = loggerFactory.CreateLogger<ProcessPantryImage>();
            _visionClient = visionClient;
        }

        /// <summary>
        /// The entry point for the "ProcessPantryImage" Azure Function.
        /// This function is triggered by an HTTP POST request.
        /// </summary>
        /// <param name="req">The incoming HTTP request data, expected to contain multipart/form-data with an image file.</param>
        /// <returns>
        /// An <see cref="HttpResponseData"/> containing a <see cref="PantryAiResponse"/>
        /// serialized to JSON.
        /// </returns>
        [Function("ProcessPantryImage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processing a pantry image request.");

            try
            {
                // Parse the Multipart/Form-Data Request
                var parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body);
                var imageFile = parsedFormBody.Files.FirstOrDefault();

                if (imageFile == null)
                {
                    _logger.LogError("No image file found in the multipart request.");
                    return await CreateJsonResponse(req,
                        new PantryAiResponse { Success = false, Error = "No image file found." },
                        HttpStatusCode.BadRequest);
                }

                _logger.LogInformation($"Received image file: {imageFile.FileName}, size: {imageFile.Data.Length} bytes");
                var imageStream = imageFile.Data;

                // Specify the features we want to extract
                VisualFeatures features =
                    VisualFeatures.Caption |
                    VisualFeatures.Objects |
                    VisualFeatures.Tags;

                ImageAnalysisResult result = await _visionClient.AnalyzeAsync(
                    BinaryData.FromStream(imageStream),
                    features);

                _logger.LogInformation("Received AI response from Azure.");

                // Transform the Response
                var mappedData = MapAiToPantryResponse(result);

                var apiResponse = new PantryAiResponse
                {
                    Success = true,
                    Data = mappedData
                };

                return await CreateJsonResponse(req, apiResponse, HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing image: {ex.Message}");
                return await CreateJsonResponse(req,
                    new PantryAiResponse { Success = false, Error = ex.Message },
                    HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Transforms the raw <see cref="ImageAnalysisResult"/> from Azure AI Vision
        /// into the custom <see cref="PantryAiData"/> format for our API.
        /// </summary>
        /// <param name="aiResult">The result object from the Azure AI Vision SDK.</param>
        /// <returns>A new <see cref="PantryAiData"/> object with mapped fields.</returns>
        private PantryAiData MapAiToPantryResponse(ImageAnalysisResult aiResult)
        {
            string itemName = "Unknown Item";
            double confidence = 0.0;
            string? description = null;
            string category = "Other";

            // Get Description (from Caption)
            if (aiResult.Caption != null && !string.IsNullOrEmpty(aiResult.Caption.Text))
            {
                description = aiResult.Caption.Text;
                _logger.LogInformation($"Mapped description: {description}");
            }

            // Get ItemName (Priority Cascade)
            // We prioritize a detected 'Object' over a generic 'Tag'
            // as 'Objects' are more specific.
            if (aiResult.Objects != null && aiResult.Objects.Values.Any())
            {
                // Find the object with the highest-confidence tag.
                var topObject = aiResult.Objects.Values
                .Where(o => o.Tags.Any()) // Ensure object has tags
                .OrderByDescending(o => o.Tags.First().Confidence) // Order by the first tag's confidence
                .FirstOrDefault();

                if (topObject != null)
                {
                    // Get name and confidence from that first tag
                    itemName = CapitalizeFirstLetter(topObject.Tags.First().Name);
                    confidence = topObject.Tags.First().Confidence;
                    _logger.LogInformation($"Mapped itemName from Object: {itemName}");
                }
            }
            // Fallback: If no objects are found, use the highest-confidence tag.
            else if (aiResult.Tags != null && aiResult.Tags.Values.Any())
            {
                var topTag = aiResult.Tags.Values.OrderByDescending(t => t.Confidence).First();
                itemName = CapitalizeFirstLetter(topTag.Name);
                confidence = topTag.Confidence;
                _logger.LogInformation($"Mapped itemName from Tag: {itemName}");
            }

            // Get Category (Inference Mapping)
            // Map known tags to our internal categories.
            var categoryMap = new Dictionary<string, string>
            {
                { "fruit", "Produce" }, { "vegetable", "Produce" }, { "apple", "Produce" },
                { "banana", "Produce" }, { "orange", "Produce" }, { "milk", "Dairy" },
                { "cheese", "Dairy" }, { "yogurt", "Dairy" }, { "beef", "Meat" },
                { "chicken", "Meat" }, { "pork", "Meat" }, { "fish", "Meat" },
                { "bread", "Bakery" }, { "cereal", "Pantry" }, { "pasta", "Pantry" },
                { "soda", "Pantry" }, { "juice", "Pantry" }, { "soft drink", "Pantry" },
                { "water", "Pantry" }
            };

            // Check all tags for a category match.
            if (aiResult.Tags != null)
            {
                foreach (var tag in aiResult.Tags.Values)
                {
                    if (categoryMap.TryGetValue(tag.Name.ToLower(), out var cat))
                    {
                        category = cat;
                        _logger.LogInformation($"Mapped category from tag '{tag.Name}': {category}");
                        break;
                    }
                }
            }

            // Construct the final data object
            return new PantryAiData
            {
                ItemName = itemName,
                Description = description,
                Category = category,
                EstimatedExpiry = null, // Not implemented
                NutritionalInfo = new { }, // Not implemented
                Confidence = confidence
            };
        }

        /// <summary>
        /// Utility method to capitalize the first letter of a string.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <returns>The string with its first letter capitalized.</returns>
        private string CapitalizeFirstLetter(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            return char.ToUpper(s[0]) + s.Substring(1);
        }

        /// <summary>
        /// Helper method to create a standardized JSON <see cref="HttpResponseData"/>.
        /// </summary>
        /// <param name="req">The original <see cref="HttpRequestData"/> used to create the response.</param>
        /// <param name="body">The object to serialize as the JSON response body.</param>
        /// <param name="statusCode">The HTTP status code for the response.</param>
        /// <returns>A new <see cref="HttpResponseData"/> configured with the JSON body and headers.</returns>
        private async Task<HttpResponseData> CreateJsonResponse(HttpRequestData req, object body, HttpStatusCode statusCode)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");

            // Use CamelCase for JSON property names to match JavaScript/frontend conventions
            var jsonBody = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await response.WriteStringAsync(jsonBody);
            return response;
        }
    }
}