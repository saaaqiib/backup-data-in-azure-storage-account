using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace backupDataInStorageAccount;

public class backupDataInStorage
{
    private readonly ILogger _logger;

    public backupDataInStorage(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<backupDataInStorage>();
    }

    [Function("syncStorageAccounts")]
    public async Task Run([TimerTrigger("0 0 12,17 * * *")] TimerInfo myTimer)
    {

        if (myTimer.IsPastDue)
        {
            _logger.LogWarning("The timer is past due!");
        }

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }

        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);

        try
        {
            string userAssignedClientId = Environment.GetEnvironmentVariable("UAMI_CLIENT_ID");
            string sourceUrl = Environment.GetEnvironmentVariable("SOURCE_ACCOUNT_URL");
            string destUrl = Environment.GetEnvironmentVariable("DEST_ACCOUNT_URL");

            if (string.IsNullOrEmpty(sourceUrl) || string.IsNullOrEmpty(destUrl))
            {
                _logger.LogError("Storage account URLs are not configured in App Settings.");
                return;
            }

            var credential = new DefaultAzureCredential(
                new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = userAssignedClientId
                });

            var sourceClient = new BlobServiceClient(new Uri(sourceUrl), credential);
            var destClient = new BlobServiceClient(new Uri(destUrl), credential);

            await foreach (BlobContainerItem container in sourceClient.GetBlobContainersAsync())
            {
                string containerName = container.Name;
                _logger.LogInformation($"Processing container: {containerName}");

                var sourceContainer = sourceClient.GetBlobContainerClient(containerName);
                var destContainer = destClient.GetBlobContainerClient(containerName);

                try
                {
                    await destContainer.CreateIfNotExistsAsync();
                    _logger.LogInformation($"Ensured container exists in destination: {containerName}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not create container {containerName}: {ex.Message}");
                }

                await foreach (BlobItem blob in sourceContainer.GetBlobsAsync())
                {
                    var sourceBlob = sourceContainer.GetBlobClient(blob.Name);
                    var destBlob = destContainer.GetBlobClient(blob.Name);

                    try
                    {
                        BlobProperties destProps = await destBlob.GetPropertiesAsync();

                        if (destProps.ETag == blob.Properties.ETag)
                        {
                            _logger.LogInformation($"Skipping unchanged blob: {blob.Name}");
                            continue;
                        }
                    }
                    catch
                    {
                        _logger.LogInformation($"Blob:{blob.Name} does not exist in the destination");
                    }

                    _logger.LogInformation($"Copying blob: {blob.Name}");
                    using var stream = await sourceBlob.OpenReadAsync();
                    await destBlob.UploadAsync(stream, overwrite: true);
                }
            }

            _logger.LogInformation("Sync completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during sync: {ex.Message}");
        }

    }
}