using CsvHelper;
using Google.Cloud.Storage.V1;
using System.Globalization;
using ReStore___backend.Services.Interfaces;
using Firebase.Auth.Provider;
using Google.Cloud.Firestore;
using Firebase.Auth.Config;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore.V1;
using ReStore___backend.Dtos;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using static ReStore___backend.Dtos.DemandPredictionDTO;
using FirebaseAdmin.Auth;
using System.Net.Mail;
using System.Net;
using FirebaseAdmin;
using ReStore___backend.Controllers;
using Firebase.Auth.Objects;
using Restore_backend_deployment_.Models;
using FirebaseAuth = FirebaseAdmin.Auth.FirebaseAuth;


namespace ReStore___backend.Services.Implementations
{
    public class DataService : IDataService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly FirebaseAuthProvider _authProvider;
        private readonly StorageClient _storageClient;
        private readonly HttpClient _httpClient;
        private readonly string _bucketName;
        private readonly string _projectId;
        private readonly string _apiUrl;
        private readonly string _renderUrl;
        private readonly string _location;
        private readonly string _endpointId;
        private readonly string _credentials;
        private readonly string _smtpEmail;
        private readonly string _smtpPassword;
        private readonly FirebaseAuth _firebaseAuth;
        private readonly ILogger<AuthController> _logger;

        public DataService(ILogger<AuthController> logger)
        {
            _httpClient = new HttpClient();
            string firebaseApiKey = Environment.GetEnvironmentVariable("FIREBASE_API_KEY");
            _bucketName = Environment.GetEnvironmentVariable("FIREBASE_BUCKET_NAME");
            _credentials = "/etc/secrets/GOOGLE_APPLICATION_CREDENTIALS";
            _smtpPassword = Environment.GetEnvironmentVariable("SMTP_EMAIL_PASSWORD");
            _smtpEmail = Environment.GetEnvironmentVariable("SMTP_EMAIL");
            _renderUrl = Environment.GetEnvironmentVariable("API_URL_RENDER");
            _logger = logger;

            // Load credentials from file explicitly
            GoogleCredential credential;
            using (var stream = new FileStream(_credentials, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream);
            }
            _storageClient = StorageClient.Create(credential);

            // Firebase Auth
            try
            {
                _authProvider = new FirebaseAuthProvider(new FirebaseConfig(firebaseApiKey));
                Console.WriteLine("Firebase Auth initialized successfully");

                FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(_credentials)
                });
                Console.WriteLine("FirebaseApp initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Firebase Auth: {ex.Message}");
            }

            // Initialize Firestore using the same credentials
            var firestoreClientBuilder = new FirestoreClientBuilder
            {
                Credential = credential
            };
            var firestoreClient = firestoreClientBuilder.Build();
            var _firebaseID = Environment.GetEnvironmentVariable("FIREBASE_STORAGE_ID");
            _firestoreDb = FirestoreDb.Create(_firebaseID, firestoreClient);
            _firebaseAuth = FirebaseAuth.DefaultInstance;
        }
        public async Task<string> SignUp(string email, string name, string username, string phoneNumber, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(phoneNumber) ||
                    string.IsNullOrWhiteSpace(password))
                {
                    return "All fields are required.";
                }

                var existingUserQuery = await _firestoreDb.Collection("Users")
                    .WhereEqualTo("email", email)
                    .GetSnapshotAsync();
                if (existingUserQuery.Count > 0)
                {
                    return "This email is already registered.";
                }

                var existingUsernameQuery = await _firestoreDb.Collection("Users")
                    .WhereEqualTo("username", username)
                    .GetSnapshotAsync();
                if (existingUsernameQuery.Count > 0)
                {
                    return "This username is already used.";
                }

                var authResult = await FirebaseAuth.DefaultInstance.CreateUserAsync(new UserRecordArgs
                {
                    Email = email,
                    Password = password,
                    DisplayName = name
                });

                string verificationLink = await _firebaseAuth.GenerateEmailVerificationLinkAsync(email);
                await SendVerificationEmailAsync(email, verificationLink);

                var userDoc = new Dictionary<string, object>
                {
                    { "email", email },
                    { "name", name },
                    { "username", username },
                    { "phone_number", phoneNumber },
                    { "password", password }
                };
                await _firestoreDb.Collection("Users").Document(authResult.Uid).SetAsync(userDoc);

                var userCred = new Dictionary<string, object>
                {
                    { "CreditsRemaining", 0 },
                    { "email", email }
                };
                await _firestoreDb.Collection("customerCredits").Document(email).SetAsync(userCred);

                return "User created successfully. Please verify your email to complete the sign-up process.";
            }
            catch (FirebaseAuthException fae)
            {
                _logger.LogError($"Firebase Auth Exception: {fae.Message}");
                return $"Error during sign-up: {fae.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during sign-up: {ex.Message}");
                return $"Unexpected error during sign-up: {ex.Message}";
            }
        }
        public async Task<(bool success, string message)> VerifyEmail(string oobCode)
        {
            if (string.IsNullOrEmpty(oobCode))
            {
                _logger.LogError("No oobCode provided for email verification.");
                return (false, "Verification code is missing.");
            }

            try
            {
                var requestBody = new { oobCode };
                var response = await _httpClient.PostAsync(
                    "https://identitytoolkit.googleapis.com/v1/accounts:update",
                    JsonContent.Create(requestBody)
                );

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Email verified successfully! You can now log in.");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Firebase verification error: {errorContent}");
                    return (false, $"Verification failed: {errorContent}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError($"HTTP request error during email verification: {httpEx.Message}");
                return (false, "An error occurred while verifying the email. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during email verification: {ex.Message}");
                return (false, "An unexpected error occurred during verification.");
            }
        }
        private async Task<string> GetUserIdFromOobCode(string oobCode)
        {
            try
            {
                var usersRef = _firestoreDb.Collection("Users");
                var querySnapshot = await usersRef.WhereEqualTo("oobCode", oobCode).GetSnapshotAsync();

                if (querySnapshot.Documents.Count > 0)
                {
                    var userDocument = querySnapshot.Documents.First();
                    return userDocument.Id; // Return the document ID (which is the user ID)
                }

                return null; // Return null if no user is found
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user ID from oobCode: {ex.Message}");
                return null;
            }
        }
        public async Task SendVerificationEmailAsync(string email, string verificationLink)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(verificationLink))
            {
                throw new ArgumentException("Email and verification link must be provided.");
            }

            try
            {
                using var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(_smtpEmail, _smtpPassword),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpEmail),
                    Subject = "Verify your Email",
                    IsBodyHtml = true,
                    Body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <div style='background-color: #00210b; color: white; padding: 20px; text-align: center; display: inline-flex;'>
                            <img src='cid:logoImage' alt='Restore Logo' style='height: 50px; margin-right: 10px;' />
                            <h2 style='margin: 0;'>Restore</h2>
                        </div>
                        <div style='padding: 20px; background-color: #f9f9f9;'>
                            <h3 style='color: #333;'>You're nearly there!</h3>
                            <p>We just need to verify your email address to complete your Restore signup.</p>
                            <div style='text-align: center; margin: 20px 0;'>
                                <a href='{verificationLink}' style='background-color: #D8FFE5; color: black; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Verify email address</a>
                            </div>
                            <p style='color: #555; font-size: 0.9em;'>Please note that this link will expire in 5 minutes.</p>
                            <p style='color: #555; font-size: 0.9em;'>If you have not signed up to Restore, please ignore this email.</p>
                        </div>                        
                        <div style='padding: 10px; text-align: center; font-size: 0.8em; color: #777;'>
                            <p>If you have any questions, feel free to contact us at <a href='mailto:restorespprt@gmail.com'>restorespprt@gmail.com</a>.</p>
                        </div>
                    </div>"
                };

                string logoPath = Path.Combine("/app", "Restore.png");
                var logo = new LinkedResource(logoPath)
                {
                    ContentId = "logoImage"
                };

                var htmlView = AlternateView.CreateAlternateViewFromString(mailMessage.Body, null, "text/html");
                htmlView.LinkedResources.Add(logo);
                mailMessage.AlternateViews.Add(htmlView);

                mailMessage.To.Add(email);

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation($"Verification email sent to {email}.");
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError($"SMTP error during email sending: {smtpEx.Message}");
                throw new InvalidOperationException("Error sending verification email: " + smtpEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during email sending: {ex.Message}");
                throw new InvalidOperationException("Unexpected error sending verification email: " + ex.Message);
            }
        }
        private async Task<bool> IsEmailVerified(string userId)
        {
            var user = await FirebaseAuth.DefaultInstance.GetUserAsync(userId);
            return user.EmailVerified;
        }
        public async Task<LoginResultDTO> Login(string email, string password)
        {
            try
            {
                // Ensure the Firebase Auth service is initialized
                if (_authProvider == null)
                {
                    throw new InvalidOperationException("Firebase Auth service is not initialized.");
                }

                // Authenticate user with Firebase Auth
                var auth = await _authProvider.SignInWithEmailAndPasswordAsync(email, password);

                if (auth.User == null)
                {
                    return new LoginResultDTO
                    {
                        Token = null,
                        Username = null,
                        Email = null,
                        ErrorMessage = "Authentication failed. Please check your email and password."
                    };
                }

                if (!await IsEmailVerified(auth.User.LocalId))
                {
                    return new LoginResultDTO
                    {
                        Token = null,
                        Username = null,
                        Email = null,
                        ErrorMessage = "Email is not verified. Please verify your email before logging in."
                    };
                }

                if (_firestoreDb == null)
                {
                    throw new InvalidOperationException("Firestore service is not initialized.");
                }

                var userId = auth.User.LocalId;

                var userDoc = await _firestoreDb.Collection("Users").Document(userId).GetSnapshotAsync();

                if (!userDoc.Exists)
                {
                    return new LoginResultDTO
                    {
                        Token = auth.FirebaseToken,
                        Email = null,
                        Username = null
                    };
                }

                var username = userDoc.GetValue<string>("username") ?? auth.User.Email;

                // Return a DTO with the token and username
                return new LoginResultDTO
                {
                    Token = auth.FirebaseToken,
                    Username = username,
                    Email = email
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login failed: {ex.Message}");
                return new LoginResultDTO
                {
                    Token = null,
                    Username = null,
                    Email= null,
                    ErrorMessage = $"Error during login: {ex.Message}"
                };
            }
        }
        public async Task SendPasswordResetEmailAsync(string email)
        {
            // Generate password reset link
            var resetLink = await FirebaseAuth.DefaultInstance.GeneratePasswordResetLinkAsync(email);

            // Send reset email
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(_smtpEmail, _smtpPassword),
                EnableSsl = true,
            };

            var emailMessage = new MailMessage
            {
                From = new MailAddress(_smtpEmail),
                Subject = "Reset your password",
                Body = $"Click the following link to reset your password: {resetLink}",
                IsBodyHtml = true,
            };

            emailMessage.To.Add(email);

            await smtpClient.SendMailAsync(emailMessage);
        }
        public async Task ProcessAndUploadDataDemands(IEnumerable<dynamic> records, string username, string email)
        {
            var recordList = records.ToList();
            var groupedRecords = recordList.GroupBy(record => record.ProductID);
            var folderPath = $"upload_demands/{username}-upload-demands/";
            await DecreaseCreditsAsync(email, 1);
            var tasks = new List<Task>();

            foreach (var group in groupedRecords)
            {
                string productId = group.Key;
                string fileName = $"product_{productId}.csv";
                var objectName = $"{folderPath}{fileName}";
                var memoryStream = new MemoryStream();

                using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(group);
                    writer.Flush();
                }

                memoryStream.Position = 0;

                tasks.Add(Task.Run(async () =>
                {
                    await _storageClient.UploadObjectAsync(_bucketName, objectName, null, new MemoryStream(memoryStream.ToArray()));
                }));

                tasks.Add(Task.Run(async () =>
                {
                    await TrainDemandModel(new MemoryStream(memoryStream.ToArray()), username);
                }));

                tasks.Add(Task.Run(async () =>
                {
                    await PredictDemand(username);
                }));
            }

            await Task.WhenAll(tasks);
        }
        public async Task<string> GetDemandDataFromStorageByUsername(string username)
        {
            var demandData = new List<dynamic>();

            // Define the path to the storage bucket and directory
            string folderPath = $"upload_demands/{username}-upload-demands/";

            // List objects in the specified folder
            var storageObjects = _storageClient.ListObjects(_bucketName, folderPath);
            foreach (var storageObject in storageObjects)
            {
                // Only process CSV files
                if (storageObject.Name.EndsWith(".csv"))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        // Download the file to memory
                        await _storageClient.DownloadObjectAsync(_bucketName, storageObject.Name, memoryStream);
                        memoryStream.Position = 0; // Reset the stream position for reading

                        using (var reader = new StreamReader(memoryStream))
                        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                        {
                            var records = csv.GetRecords<dynamic>().ToList();
                            demandData.AddRange(records); // Add records to demandData list
                        }
                    }
                }
            }

            // Group the data by ProductID
            var groupedData = demandData
                .GroupBy(record => record.ProductID) // Change "ProductID" to your desired grouping field
                .Select(group => new
                {
                    ProductID = group.Key,
                    Records = group.ToList()
                });

            // Convert the grouped data to JSON
            string jsonData = JsonConvert.SerializeObject(groupedData, Formatting.Indented);
            return jsonData;
        }
        public async Task ProcessAndUploadDataSales(IEnumerable<dynamic> records, string username, string email)
        {
            // Generate a timestamp
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmm");

            // Create a filename
            string fileName = $"sales_{timestamp}.csv";
            string folderPath = $"upload_sales/{username}-upload-sales/";

            // Decrease credits for each demand processed
            await DecreaseCreditsAsync(email, 1); // Deduct 1 credit

            // Save the entire records to a MemoryStream
            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    // Write all records to the CSV writer
                    csv.WriteRecords(records);
                }

                // Reset the position of the memory stream to the beginning
                memoryStream.Position = 0;

                // Define the object name for cloud storage
                var objectName = $"{folderPath}{fileName}";

                // Upload the CSV file to Cloud Storage
                await _storageClient.UploadObjectAsync(_bucketName, objectName, null, memoryStream);

                // Create copies of the memory stream to prevent "closed stream" issues
                memoryStream.Position = 0;
                var trainModelStream = new MemoryStream();
                var salesInsightStream = new MemoryStream();

                // Copy the data to the new streams
                await memoryStream.CopyToAsync(trainModelStream);
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(salesInsightStream);

                // Reset the positions of the new streams
                trainModelStream.Position = 0;
                salesInsightStream.Position = 0;

                // Call TrainModel using the copied stream
                await TrainSalesModel(trainModelStream, username);

                // Pass the copied memory stream to SalesInsight
                await SalesInsight(salesInsightStream, username);
            }
        }
        public async Task<string> GetSalesDataFromStorageByUsername(string username)
        {
            var salesData = new List<SaleRecordDTO>();

            // Define the path to the storage bucket and directory without the trailing slash
            string folderPath = $"upload_sales/{username}-upload-sales";

            // Get the list of objects in the specified folder
            var files = _storageClient.ListObjects(_bucketName, folderPath).ToList();

            // Filter for CSV files
            var csvFiles = files.Where(file => file.Name.EndsWith(".csv")).ToList();

            // Ensure there are CSV files
            if (!csvFiles.Any())
            {
                throw new Exception("No CSV files found in the specified folder.");
            }

            // Get the latest sales file based on the count of CSV files
            var latestFile = csvFiles.Count == 1 ? csvFiles[0] : csvFiles.Last();

            Console.WriteLine($"File name: {latestFile.Name}");

            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    // Download the file to memory
                    await _storageClient.DownloadObjectAsync(_bucketName, latestFile.Name, memoryStream);
                    memoryStream.Position = 0;

                    // Convert the file content to string (assuming it's in a supported format, like CSV)
                    using (var reader = new StreamReader(memoryStream))
                    {
                        string content = await reader.ReadToEndAsync();

                        // Split the CSV content into lines
                        var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                        // Ensure we have lines to process
                        if (lines.Length > 1)
                        {
                            // Get the header row
                            var header = lines[0].Split(',');

                            // Check if the header at index 1 is not "Sales" and rename it if necessary
                            if (header.Length > 1 && header[1].Trim() != "Sales")
                            {
                                header[1] = "Sales"; // Rename the second column to 'Sales'
                            }
                            else if (header.Length < 2)
                            {
                                throw new Exception("The CSV file does not have enough columns.");
                            }

                            // Skip the header and parse the data starting from the second line
                            for (int i = 1; i < lines.Length; i++)
                            {
                                var columns = lines[i].Split(',');

                                // Check if the row has the correct number of columns
                                if (columns.Length >= 2 && !string.IsNullOrWhiteSpace(columns[0]))
                                {
                                    try
                                    {
                                        // Parse the year and month from the first column
                                        var dateParts = columns[0].Split('-');
                                        if (dateParts.Length != 2) continue;

                                        int year = int.Parse(dateParts[0]);
                                        int month = int.Parse(dateParts[1]);

                                        // Parse the sales amount from the second column (now ensured to be "Sales")
                                        decimal sales = decimal.Parse(columns[1]);

                                        // Create a SaleRecord object and add it to the list
                                        salesData.Add(new SaleRecordDTO
                                        {
                                            Year = year,
                                            Month = month,
                                            Sales = sales
                                        });
                                    }
                                    catch (FormatException ex)
                                    {
                                        Console.WriteLine($"Skipping invalid line: {lines[i]} - Error: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("No valid data rows found in the CSV file.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception details
                Console.WriteLine($"Error processing file: {ex.Message}");
            }

            // Create the desired JSON structure
            var result = salesData
                .GroupBy(sale => sale.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    SalesData = g.Select(s => new
                    {
                        Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(s.Month), // Convert month number to name
                        Sales = s.Sales
                    }).ToList()
                }).ToList();

            // Create a final object to return as JSON
            var finalResult = new { data = result };

            // Serialize to JSON without unwanted characters or newlines
            string jsonData = JsonConvert.SerializeObject(finalResult, Formatting.None);

            // Return the clean JSON string
            return jsonData.Replace("\\n", ""); // Remove any unwanted newlines from the JSON string
        }
        public async Task<string> SalesInsight(MemoryStream salesData, string username)
        {
            // Call your API to get the insights
            string insights;

            // Remove the using statement for _httpClient
            var formData = new MultipartFormDataContent();
            formData.Add(new StreamContent(salesData), "file", "sales_data.csv");

            string apiUrl = Environment.GetEnvironmentVariable("API_URL");
            if (string.IsNullOrEmpty(apiUrl))
            {
                throw new InvalidOperationException("API_URL environment variable is not set.");
            }

            string insightUrl = $"{apiUrl}/generate-insights";
            var response = await _httpClient.PostAsync(insightUrl, formData);
            insights = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API call failed: {insights}");
            }

            // Extract the insights text from the JSON response
            var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, string>>(insights);
            if (!jsonObject.ContainsKey("insights"))
            {
                throw new Exception("Insights not found in the API response.");
            }

            string insightsText = jsonObject["insights"];

            // Format the insights text
            insightsText = insightsText
                .Replace("\n", " ") // Replace new lines with space
                .Replace("\r", " ") // Replace carriage returns with space
                .Replace("\"", "")  // Remove any quotes
                .Replace(",", " ")   // Replace commas with space
                .Replace("  ", " "); // Replace double spaces with a single space

            Console.WriteLine(insightsText); // Log the formatted insights

            // Convert insights to CSV format
            var csvLines = new List<string>
    {
        "InsightData" // Header
    };

            // Add the cleaned insights as the body of the CSV
            csvLines.Add(insightsText);

            string csvContent = string.Join("\n", csvLines);

            Console.WriteLine(csvContent);

            // Upload the CSV to Firebase Storage
            var storagePath = $"Insight/{username}-sales-insight/sales_insights.csv"; // Define your storage path
            using (var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent)))
            {
                await _storageClient.UploadObjectAsync(_bucketName, storagePath, "text/csv", uploadStream);
            }

            return insightsText; // Optionally return the cleaned insights text
        }
        public async Task<string> GetSalesInsightByUsername(string username)
        {
            var insightData = new List<InsightDTO>();

            // Define the path to the storage bucket and directory
            string folderPath = $"Insight/{username}-sales-insight/";
            Console.WriteLine($"Folder path: {folderPath}");

            // Get the list of objects in the specified folder
            var files = _storageClient.ListObjects(_bucketName, folderPath).ToList();

            // Filter for CSV files
            var csvFiles = files.Where(file => file.Name.EndsWith(".csv")).ToList();

            // Ensure there are CSV files
            if (!csvFiles.Any())
            {
                throw new Exception("No CSV files found in the specified folder.");
            }

            // Log the list of files
            Console.WriteLine("Files in the directory: ");
            foreach (var file in files)
            {
                Console.WriteLine($"- {file.Name}");
            }

            // Get the latest sales insight file based on the count of CSV files
            var latestFile = csvFiles[0];

            Console.WriteLine($"File name: {latestFile.Name}");

            using (var memoryStream = new MemoryStream())
            {
                // Download the latest file from cloud storage
                await _storageClient.DownloadObjectAsync(_bucketName, latestFile.Name, memoryStream);
                memoryStream.Position = 0; // Reset stream position

                // Convert CSV data to JSON
                using (var reader = new StreamReader(memoryStream))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    var records = csv.GetRecords<InsightDTO>().ToList();
                    insightData.AddRange(records);
                }
            }

            // Convert the list to JSON and return
            return JsonConvert.SerializeObject(insightData);
        }
        public async Task<string> PredictDemand(string username)
        {
            try
            {
                using (var form = new MultipartFormDataContent())
                {
                    form.Add(new StringContent(username), "username");

                    string baseUrl = Environment.GetEnvironmentVariable("API_URL");

                    // Send the request to predict demand
                    HttpResponseMessage response = await _httpClient.PostAsync(baseUrl + "/predict_demand", form);
                    response.EnsureSuccessStatusCode(); // Throws if the status code is not 2xx

                    // Read the response content
                    var content = await response.Content.ReadAsStringAsync();

                    // Check if the response contains an error
                    var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                    if (jsonResponse.ContainsKey("error"))
                    {
                        string errorMessage = jsonResponse["error"];
                        return $"Error during demand prediction: {errorMessage}";
                    }

                    // Check if the response is formatted as a JSON array
                    if (content.Trim().StartsWith("[") && content.Trim().EndsWith("]"))
                    {
                        // Optional: Deserialize the response into a list of predicted demand objects
                        var predictedDemands = JsonConvert.DeserializeObject<List<DemandPredictionDTO>>(content);

                        // Process the predicted demands as needed
                        return "Demand prediction success";
                    }
                    else
                    {
                        return "Failed to predict demand: Invalid response format.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error during demand prediction: {ex.Message}";
            }
        }
        public async Task<string> TrainDemandModel(MemoryStream file, string username)
        {
            Console.WriteLine("Calling Train Demand Model");
            Console.WriteLine($"MemoryStream with {username}");

            if (file == null || file.Length == 0)
            {
                return "Invalid CSV file.";
            }

            try
            {
                using (var form = new MultipartFormDataContent())
                {
                    var streamContent = new StreamContent(file);
                    streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");

                    // Add the MemoryStream as a CSV file in the form
                    form.Add(streamContent, "file", $"{username}_file.csv"); // Use username for clarity

                    // Add the username
                    form.Add(new StringContent(username), "username");

                    string baseUrl = Environment.GetEnvironmentVariable("API_URL");

                    // Send the request to train the model
                    HttpResponseMessage response = await _httpClient.PostAsync(baseUrl + "/train_demand_model", form);
                    response.EnsureSuccessStatusCode();

                    // Read the response content
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                    string resultMessage = jsonResponse.ContainsKey("message") ? jsonResponse["message"] : jsonResponse["error"];

                    if (resultMessage.Equals("error", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Failed to train demand model";
                    }

                    return "Training demand model success, ";
                }
            }
            catch (Exception ex)
            {
                return $"Error during model training: {ex.Message}";
            }
        }
        public async Task<string> TrainSalesModel(MemoryStream file, string username)
        {
            try
            {
                // Retrieve the base URL from the environment variable
                string baseUrl = Environment.GetEnvironmentVariable("API_URL");

                if (string.IsNullOrEmpty(baseUrl))
                {
                    return "Base URL is not set.";
                }

                // Use MultipartFormDataContent to send the file and username
                using (var form = new MultipartFormDataContent())
                {
                    // Add the MemoryStream as file content with a proper filename
                    form.Add(new StreamContent(file), "file", "sales_data.csv");
                    form.Add(new StringContent(username), "username");

                    // Send the request to train the model
                    HttpResponseMessage response = await _httpClient.PostAsync($"{baseUrl}/train_model", form);
                    response.EnsureSuccessStatusCode();

                    // Read the response content
                    var content = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);

                    // Check if the response contains a success message
                    string resultMessage = jsonResponse.ContainsKey("message") ? jsonResponse["message"] : jsonResponse["error"];

                    if (resultMessage.Equals("error", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Failed to train sales model";
                    }

                    return "Training sales model success";
                }
            }
            catch (Exception ex)
            {
                return $"Error during model training: {ex.Message}";
            }
        }
        public async Task<string> GetSalesPrediction(string username)
        {
            try
            {
                // Define the path to the storage bucket and directory without the trailing slash
                string folderPath = $"AI Model/Sales_Prediction/{username}-sales-prediction/";

                // List objects in the specified folder
                var files = _storageClient.ListObjectsAsync(_bucketName, folderPath);

                // Fetch the first file found
                var fileObject = await files.FirstOrDefaultAsync();

                if (fileObject == null)
                {
                    Console.WriteLine("No files found in the specified folder.");
                    return null;
                }

                string filePath = fileObject.Name; // Get the full path of the found file

                // Download the CSV file from Firebase Storage
                using (var memoryStream = new MemoryStream())
                {
                    Console.WriteLine("Fetching file from Firebase Storage...");

                    // Download the file to memory
                    await _storageClient.DownloadObjectAsync(_bucketName, filePath, memoryStream);

                    // Reset the memory stream position
                    memoryStream.Position = 0;

                    // Convert CSV to JSON
                    using (var reader = new StreamReader(memoryStream))
                    using (var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
                    {
                        var records = csv.GetRecords<dynamic>(); // Use a dynamic type or create a model class
                        var jsonResult = JsonConvert.SerializeObject(records);

                        Console.WriteLine("File downloaded and converted to JSON successfully.");
                        return jsonResult;
                    }
                }
            }
            catch (Google.GoogleApiException ex)
            {
                Console.WriteLine($"Error fetching file: {ex.Message}");
                Console.WriteLine($"Status Code: {ex.Error.Code}");
                Console.WriteLine($"Reason: {ex.Error.Message}");
                return null;
            }
            catch (Exception ex)
            {
                return $"Error in fetching and processing sales data: {ex.Message}";
            }
        }
        public async Task<string> GetDemandPrediction(string username)
        {
            try
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(3000);

                string baseUrl = Environment.GetEnvironmentVariable("API_URL");
                var requestUri = new Uri(new Uri(baseUrl), $"/demand_prediction?username={username}");
                Console.WriteLine($"URL : {requestUri}");

                HttpResponseMessage response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                // Log content for debugging purposes
                Console.WriteLine($"Raw Content: {content}");

                // Handle potential double encoding
                if (content.StartsWith("\"") && content.EndsWith("\""))
                {
                    content = content.Trim('\"').Replace("\\\"", "\"");  // Unescape inner JSON
                }

                string formatOutput = FormatDemandPredictionOutput(content);
                return formatOutput;
            }
            catch (Exception ex)
            {
                return $"Error in fetching demand prediction data: {ex.Message}";
            }
        }
        public string FormatDemandPredictionOutput(string jsonInput)
        {
            try
            {
                // Trim any leading or trailing whitespace
                jsonInput = jsonInput.Trim();

                // Remove the outer quotes if they exist
                if (jsonInput.StartsWith("\"") && jsonInput.EndsWith("\""))
                {
                    jsonInput = jsonInput[1..^1]; // Remove the first and last characters
                }

                // Replace escaped quotes with actual quotes
                jsonInput = jsonInput.Replace("\\\"", "\"");

                // Check for any extraneous characters at the end
                if (jsonInput.Length > 0 && jsonInput[^1] == ']')
                {
                    // Deserialize the cleaned JSON string into a list of DemandPrediction objects
                    var demandPredictions = JsonConvert.DeserializeObject<List<DemandPrediction>>(jsonInput);

                    // Serialize the list back to a JSON string with proper formatting
                    var formattedJsonOutput = JsonConvert.SerializeObject(demandPredictions, Formatting.Indented);

                    return formattedJsonOutput;
                }
                else
                {
                    return "Error: Invalid JSON format.";
                }
            }
            catch (JsonException ex)
            {
                return $"Error in formatting demand prediction data: {ex.Message}";
            }
        }
        public async Task DecreaseCreditsAsync(string email, int amount)
        {
            DocumentReference docRef = _firestoreDb.Collection("customerCredits").Document(email);
            var snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var currentCredits = snapshot.GetValue<int>("CreditsRemaining");

                // Check if the user has enough credits
                if (currentCredits >= amount)
                {
                    // Update the credits
                    await docRef.UpdateAsync("CreditsRemaining", currentCredits - amount);
                }
                else
                {
                    throw new InvalidOperationException("Insufficient credits.");
                }
            }
            else
            {
                throw new InvalidOperationException("User not found.");
            }
        }
        public async Task SaveCustomerCreditsAsync(string email, int credits)
        {
            try
            {
                CollectionReference creditsCollection = _firestoreDb.Collection("customerCredits");
                DocumentReference documentReference = creditsCollection.Document(email);

                // Find the document
                DocumentSnapshot documentSnapshot = await documentReference.GetSnapshotAsync();

               
                int existingCredits = documentSnapshot.GetValue<int>("CreditsRemaining");
                int updatedCredits = existingCredits + credits;

                var updates = new Dictionary<string, object>
                {
                    { "email", email },
                    { "CreditsRemaining", updatedCredits }
                };

                await documentReference.UpdateAsync(updates);
                Console.WriteLine("Customer credits updated successfully.");
               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving customer credits: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }
        public async Task<int> GetCustomerCreditsAsync(string email)
        {
            try
            {
                CollectionReference creditsCollection = _firestoreDb.Collection("customerCredits");
                DocumentReference documentReference = creditsCollection.Document(email);

                // Get the document snapshot
                DocumentSnapshot documentSnapshot = await documentReference.GetSnapshotAsync();

                if (documentSnapshot.Exists)
                {
                    // If the document exists, retrieve the CreditsRemaining
                    int creditsRemaining = documentSnapshot.GetValue<int>("CreditsRemaining");
                    Console.WriteLine($"Customer credits retrieved successfully for {email}: {creditsRemaining}");
                    return creditsRemaining;
                }
                else
                {
                    Console.WriteLine($"No credits found for email: {email}");
                    return 0; 
                }
            }
            catch (Exception ex)
            {                
                Console.WriteLine($"Error retrieving customer credits: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }
        public async Task SavePaymentReceiptAsync(PaymentReceipt receipt)
        {
            CollectionReference receiptsCollection = _firestoreDb.Collection("paymentReceipts");
            await receiptsCollection.AddAsync(receipt);
        }
    }
}