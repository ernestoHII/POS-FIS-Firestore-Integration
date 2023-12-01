using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Newtonsoft.Json;

class Program
{
    static FirestoreDb firestoreDb;
    static bool isUpdatingFirestore = false; // Flag to indicate Firestore update mode
    static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    // Read from POS config coz' not all clients have the same Database name.
    static readonly string connectionString = "Server=localhost;Database=easypos;User Id=notifman;Password=root1234;";

    static async Task Main()
    {

        Program program = new Program();
        program.CheckConnection();
        Console.WriteLine("Passed 1");
        program.CheckPermission();
        Console.WriteLine("Passed 2");
        program.CheckServiceBroker();
        Console.WriteLine("Passed 3");
        program.SetupSqlDependency();
        Console.WriteLine("Passed 4");
        program.InitialFetchData();
        Console.WriteLine("Passed 5");
        /*Environment.Exit(0);*/

        // Sets the Google application credentials for Firebase access
        SetGoogleApplicationCredentials();
        // Initializes the Firebase application with these credentials
        InitializeFirebase();
        // Creates a Firestore database instance for a specific Firebase project
        firestoreDb = FirestoreDb.Create("hii-pos-330e9"); // Replace with your Firebase project ID
                                                           // A list of document IDs to listen for changes
        List<string> documentIds = new List<string> { "BranchID-19-FIS", "Added-Item", "Updated-Item" }; // Replace with your document IDs
                                                                           // Sets up listeners for each document in the list
        foreach (var docId in documentIds)
        {
            ListenToDocumentChanges(docId);
        }
        Console.WriteLine("Listening for updates. Press Enter to exit.");
        // Awaits indefinitely until the cancellation token is cancelled
        await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
    }

    static void StockIn()
    {
        int stockInId = 500; // Example parameter, replace with actual value if needed

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            try
            {
                conn.Open();

                string sql = @"
                exec sp_executesql N'SELECT [t4].[Id], [t4].[StockInId], [t4].[ItemId], [t4].[BarCode] AS [ItemBarcode], [t4].[ItemDescription], [t4].[UnitId], [t4].[Unit], [t4].[Quantity], [t4].[Cost], [t4].[Amount], [t4].[value], [t4].[value2], CONVERT(NVarChar(MAX),[t4].[value3]) AS [value3], [t4].[value4] AS [LotNumber], [t4].[AssetAccountId], [t4].[Account] AS [AssetAccount], [t4].[Price]
                FROM (
                    SELECT [t0].[Id], [t0].[StockInId], [t0].[ItemId], [t1].[BarCode], [t1].[ItemDescription], [t0].[UnitId], [t2].[Unit], [t0].[Quantity], [t0].[Cost], [t0].[Amount], 
                    (CASE 
                        WHEN [t0].[ExpiryDate] IS NOT NULL THEN 1
                        ELSE 0
                     END) AS [value], [t0].[ExpiryDate] AS [value2], @p0 AS [value3], COALESCE([t0].[LotNumber],@p1) AS [value4], [t0].[AssetAccountId], [t3].[Account], [t0].[Price]
                FROM [dbo].[TrnStockInLine] AS [t0]
                INNER JOIN [dbo].[MstItem] AS [t1] ON [t1].[Id] = [t0].[ItemId]
                INNER JOIN [dbo].[MstUnit] AS [t2] ON [t2].[Id] = [t0].[UnitId]
                INNER JOIN [dbo].[MstAccount] AS [t3] ON [t3].[Id] = [t0].[AssetAccountId]
                ) AS [t4]
                WHERE [t4].[StockInId] = @p2',N'@p0 nvarchar(4000),@p1 nvarchar(4000),@p2 int',@p0=N'',@p1=N'',@p2=@stockInId";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    // Add parameters
                    cmd.Parameters.Add("@stockInId", SqlDbType.Int).Value = stockInId;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Process your data here...
                            Console.WriteLine(reader["Id"].ToString());
                            // Add other fields as needed
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }
    }
    static void SetGoogleApplicationCredentials()
    {
        // Gets the current directory of the executable
        string workingDir = Directory.GetCurrentDirectory();
        // Constructs the path to the Firebase service account key file
        string pathToServiceAccountKeyFile = Path.Combine(workingDir, "hii-pos-330e9-firebase-adminsdk-jjeto-43862bd4d4.json");
        // Sets the path as an environment variable for Google application credentials
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", pathToServiceAccountKeyFile);
    }

    static void InitializeFirebase()
    {
        // Creates a new Firebase application instance with the default credentials
        FirebaseApp.Create(new AppOptions()
        {
            Credential = GoogleCredential.GetApplicationDefault(),
        });
    }

    static void ListenToDocumentChanges(string documentId)
    {
        // References a specific document in the Firestore database
        DocumentReference docRef = firestoreDb.Collection("StockIn").Document(documentId);
        try
        {
            // Sets up a listener for changes to the specified document
            docRef.Listen(async snapshot =>
            {
                Console.WriteLine($"Document {documentId} has been updated.");

                // Checks if updates to Firestore are not currently being made by this program
                if (!isUpdatingFirestore)
                {
                    // Displays a notification when the document is updated
                    ShowNotification($"Document {documentId} has been updated.", "Update");

                    // Processes the updated data if the 'Items' field is present
                    if (snapshot.ContainsField("Items"))
                    {
                        // Deserialize the JSON data to a dictionary of items
                        string jsonItems = snapshot.GetValue<string>("Items");
                        if (!string.IsNullOrEmpty(jsonItems))
                        {
                            Dictionary<string, Item> itemsDictionary = JsonConvert.DeserializeObject<Dictionary<string, Item>>(jsonItems);

                            // Iterates through each item and prints details
                            foreach (var kvp in itemsDictionary)
                            {
                                string itemName = kvp.Key;
                                Item item = kvp.Value;

                                // Printing item details
                                Console.WriteLine($"Item Name: {itemName}");
                                Console.WriteLine($"Item Id: {item.Id}");
                                Console.WriteLine($"Item JOId: {item.JOId}");
                                Console.WriteLine($"Item ItemId: {item.ItemId}");
                                Console.WriteLine($"Item Particulars: {item.Particulars}");
                                Console.WriteLine($"Item Quantity: {item.Quantity}");
                                Console.WriteLine($"Item BaseQuantity: {item.BaseQuantity}");
                                Console.WriteLine($"Item Cost: {item.Cost}");
                                Console.WriteLine($"Item Amount: {item.Amount}");
                                Console.WriteLine();
                            }

                            // Example of updating Firestore in response to changes
                            string newRemarks = "POS has updated Firestore";
                            Console.WriteLine($"POS has updated Firestore");
                            await UpdateRemarksInFirestore(documentId, newRemarks);
                        }
                    }
                    else
                    {
                        // Logs if the 'items' field is not found in the document
                        Console.WriteLine($"'items' field not found in document {documentId}");
                    }
                }
                else
                {
                    // Ignores the update if it was initiated by this program
                    Console.WriteLine($"Firestore update initiated by code. Ignoring notification.");
                }
            });
        }
        catch (Exception ex)
        {
            // Logs any errors encountered while setting up the listener
            Console.WriteLine($"Error setting up listener for {documentId}: {ex.Message}");
        }
    }

    static async Task UpdateRemarksInFirestore(string documentId, string newRemarks)
    {
        // Enables Firestore update mode to prevent recursive updates
        isUpdatingFirestore = true;

        // References the same document in Firestore
        DocumentReference docRef = firestoreDb.Collection("StockIn").Document(documentId);

        // Prepares the data to be updated
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "Remarks", newRemarks }
        };

        // Updates the document in Firestore with the new data
        await docRef.UpdateAsync(data);

        // Logs that the remarks were successfully updated
        Console.WriteLine($"Remarks updated in Firestore for document {documentId}");

        // Disables Firestore update mode after the update
        isUpdatingFirestore = false;
    }

    static void ShowNotification(string message, string title)
    {
        // Shows a message box as a notification with the specified message and title
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CheckConnection()
    {

        // Attempt to open a connection to the database
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open(); // Try to open the connection
                             // If successful, the connection will be closed when exiting the using block
            }

            // Other code that should run after successful connection...
        }
        catch (SqlException ex)
        {
            MessageBox.Show($"Failed to connect to the database: {ex.Message}", "Database Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1); // Exit the application if the connection is not successful
        }
    }

    private void CheckPermission()
    {
        // Make sure client has permissions 
        try
        {
            SqlClientPermission perm = new SqlClientPermission(System.Security.Permissions.PermissionState.Unrestricted);
            perm.Demand();
        }
        catch
        {
            throw new ApplicationException("No permission");
        }
    }

    private void CheckServiceBroker()
    {
        // Check if Service Broker is enabled
        bool serviceBrokerEnabled = IsServiceBrokerEnabled();
        if (!serviceBrokerEnabled)
        {
            Console.WriteLine(serviceBrokerEnabled);
            MessageBox.Show("Service Broker is not enabled. The application will now exit.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(0);
            return; // Ensures that the rest of the constructor code is not executed
        }
        else
        {
            Console.WriteLine("Service Broker is enabled.", serviceBrokerEnabled);
        }
    }

    private bool IsServiceBrokerEnabled()
    {
        string commandText = "SELECT is_broker_enabled FROM sys.databases WHERE name = 'easypos';";
        bool isBrokerEnabled = false;

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            SqlCommand command = new SqlCommand(commandText, connection);

            try
            {
                connection.Open();
                object result = command.ExecuteScalar();

                if (result != null)
                {
                    isBrokerEnabled = Convert.ToBoolean(result);
                    Console.WriteLine("Service Broker enabled status: " + isBrokerEnabled);
                }
                else
                {
                    Console.WriteLine("Service Broker status check returned no results.");
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine("SQL Error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        return isBrokerEnabled;
    }

    private void SetupSqlDependency()
    {
        // Start the SqlDependency listener.
        SqlDependency.Start(connectionString);
    }

    private void InitialFetchData()
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand("SELECT Remarks, Id FROM dbo.TrnStockIn", connection))
                {
                    var dependency = new SqlDependency(command);
                    dependency.OnChange += new OnChangeEventHandler(OnDataChanged);
                    command.ExecuteReader();
                }
            }
        }
        catch (SqlException ex)
        {
            Console.WriteLine($"Error in database operation: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General error: {ex.Message}");
        }
    }

    private void OnDataChanged(object sender, SqlNotificationEventArgs e)
    {
        // Use pattern matching to check and cast sender to SqlDependency
        if (sender is SqlDependency dependency)
        {
            dependency.OnChange -= OnDataChanged;
        }

        if (e.Type == SqlNotificationType.Change)
        {
            MessageBox.Show("Changes were made to the TrnStockIn table.");
            FetchData(); // Assuming FetchData() only modifies controls
        }
    }


    private void FetchData()
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand("SELECT Remarks, Id FROM dbo.TrnStockIn", connection))
                {
                    // Setup the SQL dependency
                    var dependency = new SqlDependency(command);
                    dependency.OnChange += new OnChangeEventHandler(OnDataChanged);

                    // Execute the command to establish the dependency
                    command.ExecuteReader();
                    /*DisplayToTable();*/
                }
            }
        }
        catch (SqlException ex)
        {
            Console.WriteLine($"Error in database operation: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"General error: {ex.Message}");
        }
    }
}


public class Item
{
    // Defines a class representing an item with various properties
    public int Id { get; set; }
    public string JOId { get; set; }
    public int ItemId { get; set; }
    public string Particulars { get; set; }
    public double Quantity { get; set; }
    public double BaseQuantity { get; set; }
    public double Cost { get; set; }
    public double Amount { get; set; }
}


