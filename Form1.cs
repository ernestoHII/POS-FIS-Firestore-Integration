using System;
using System.Collections.Generic;
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

    static async Task Main()
    {
        // Sets the Google application credentials for Firebase access
        SetGoogleApplicationCredentials();
        // Initializes the Firebase application with these credentials
        InitializeFirebase();
        // Creates a Firestore database instance for a specific Firebase project
        firestoreDb = FirestoreDb.Create("hii-pos-330e9"); // Replace with your Firebase project ID
        // A list of document IDs to listen for changes
        List<string> documentIds = new List<string> { "BranchID-19" }; // Replace with your document IDs
        // Sets up listeners for each document in the list
        foreach (var docId in documentIds)
        {
            ListenToDocumentChanges(docId);
        }
        Console.WriteLine("Listening for updates. Press Enter to exit.");
        // Awaits indefinitely until the cancellation token is cancelled
        await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
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
}
