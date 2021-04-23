using Microsoft.Hadoop.Avro;
using Microsoft.Hadoop.Avro.Container;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using SmartCafe.EventHub.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCafe.EventHub.CaptureReceiver
{
    class Program
    {
        const string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=smartcafecapturemc;AccountKey=rFedJZ1aBHKmMBoscOaiLMxnXyI73r1JoU68j4Rt5HB7lTa24qm8R4+Buug1zzDkFVEgwjB227Aqc9yDnv3w4Q==;EndpointSuffix=core.windows.net";
        const string containerName = "eventhubcapture";
        static void Main(string[] args)
        {
            MainAsync().Wait();
        }

        private static async Task MainAsync()
        {
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference(containerName);

            var resultSegment = await blobContainer.ListBlobsSegmentedAsync(
                null, true, BlobListingDetails.All, null, null, null, null);

            // listing filenames
            foreach (var cloudBlockBlob in resultSegment.Results.OfType<CloudBlockBlob>())
            {
                //Console.WriteLine(cloudBlockBlob.Name);
                await ProcessCloudBlockBlobAsync(cloudBlockBlob);

                // Log just a single file
                //break;
            }

            Console.ReadLine();
        }

        private static async Task ProcessCloudBlockBlobAsync(CloudBlockBlob cloudBlockBlob)
        {
            //var avroText = await cloudBlockBlob.DownloadTextAsync();
            //Console.WriteLine(avroText);

            // bug Azure CloudBlockBlob
            if (cloudBlockBlob.Properties.Length != 508)
            {
                var avroRecords = await DownloadAvroRecordsAsync(cloudBlockBlob);
                PrintCoffeeMachineDatas(avroRecords);
            }

            //cloudBlockBlob.DeleteAsync
        }

        private static void PrintCoffeeMachineDatas(List<AvroRecord> avroRecords)
        {
            var coffeeMachineDatas = avroRecords.Select(avroRecord =>
                    CreateCoffeeMachineData(avroRecord));
            foreach (var coffeeMachineData in coffeeMachineDatas)
            {
                Console.WriteLine(coffeeMachineData);
            }
        }

        private static async Task<List<AvroRecord>> DownloadAvroRecordsAsync(CloudBlockBlob cloudBlockBlob)
        {
            var memoryStream = new MemoryStream();
            await cloudBlockBlob.DownloadToStreamAsync(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            List<AvroRecord> avroRecords;
            using (var reader = AvroContainer.CreateGenericReader(memoryStream))
            {
                using (var sequentialReader = new SequentialReader<object>(reader))
                {
                    avroRecords = sequentialReader.Objects.OfType<AvroRecord>().ToList();
                }
            }

            return avroRecords;
        }

        private static CoffeeMachineData CreateCoffeeMachineData(AvroRecord avroRecord)
        {
            var body = avroRecord.GetField<byte[]>("Body");
            var dataAsJson = Encoding.UTF8.GetString(body);
            var coffeeMachineData = JsonConvert.DeserializeObject<CoffeeMachineData>(dataAsJson);
            return coffeeMachineData;
        }
    }
}
