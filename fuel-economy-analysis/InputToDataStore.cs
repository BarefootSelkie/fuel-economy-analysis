using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
//using Newtonsoft.Json;
using Microsoft.VisualBasic;
using CsvHelper;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace fuel_economy_analysis
{
    public static class InputToDataStore
    {
        [FunctionName("InputToDataStore")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Blob("azure-webjobs-hosts/fueldata.csv", FileAccess.Read, Connection = "")] Stream fileIn,
            [Blob("azure-webjobs-hosts/fueldata.csv", FileAccess.Write, Connection = "")] Stream fileOut,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string error = "Error: 400";
            bool failed = false;
            string secKey = "Change";

            // Get data from the query 

            string key = req.Query["key"];
            string dateIn = req.Query["date"];
            string odoIn = req.Query["odo"];
            string carMPGIn = req.Query["carMPG"];
            string fuelIn = req.Query["fuel"];

            // Get data from the body

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            if (!string.IsNullOrEmpty(requestBody))
            {
                var data = JsonSerializer.Deserialize<requestDesign>(requestBody);

                key = key ?? data?.key;
                dateIn = dateIn ?? data?.date;
                odoIn = odoIn ?? data?.odo;
                carMPGIn = carMPGIn ?? data?.carMPG;
                fuelIn = fuelIn ?? data?.fuel;
            }

            // Convert data from strings and checks data is okay

            DateTime date;
            decimal odo, carMPG, fuel;

            if (key != secKey)
            {
                failed = true;
                error += System.Environment.NewLine + "Invalid Key";
            }

            if (!DateTime.TryParse(dateIn, out date))
            {
                failed = true;
                error += System.Environment.NewLine + "Date is invalid or not iso date format";
            }

            if (!decimal.TryParse(odoIn, out odo) || odo < 0 || odo > 60000)
            {
                failed = true;
                error += System.Environment.NewLine + "ODO should be a number between 0 and 60,000";
            }
            if (!decimal.TryParse(carMPGIn, out carMPG) || carMPG < 0 || carMPG > 100)
            {
                failed = true;
                error += System.Environment.NewLine + "MPG should be a number between 0 and 100";
            }
            if (!decimal.TryParse(fuelIn, out fuel) || fuel < 0 || fuel > 100)
            {
                failed = true;
                error += System.Environment.NewLine + "Fuel should be a number between 0 and 100";
            }
            
            string responseMessage;

            if (!failed)
            {
                using (StreamReader reader = new StreamReader(fileIn))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    // Load the csv file into a variable

                    var dataStore = csv.GetRecords<recordDesign>().ToList();

                    // Create a working memory record

                    recordDesign record = new recordDesign();

                    // Do the calcuations

                    record.date = date;
                    record.carODO = odo;
                    record.calcDays = Convert.ToInt32((date - dataStore.Last().date).TotalDays);
                    record.calcMiles = odo - dataStore.Last().carODO;
                    record.calcKm = MilestoKM(record.calcMiles);
                    record.calcKmD = record.calcKm / record.calcDays;
                    record.carKPL = MPGtoKPL(carMPG);
                    record.calcKPL = record.calcKm / fuel;
                    record.varKPL = record.carKPL - record.calcKPL;
                    record.carL100 = MPGtoL100(carMPG);
                    record.calcL100 = (fuel / record.calcKm) * 100M;
                    record.varL100 = record.carL100 - record.calcL100;

                    // Return recieved data and calcuated data for debugging

                    string recordEcho = JsonSerializer.Serialize(record);

                    responseMessage =
                        "{" + System.Environment.NewLine +
                        "\"request\":" + System.Environment.NewLine +
                        requestBody + System.Environment.NewLine +
                        "," + System.Environment.NewLine +
                        "\"calcuated\":" + System.Environment.NewLine +
                        recordEcho + System.Environment.NewLine +
                        "}";

                    dataStore.Add(record);

                    // Add new line to csv file with new data
                    using (var writer = new StreamWriter(fileOut))
                    using (var csvOut = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csvOut.Configuration.HasHeaderRecord = true;
                        csvOut.WriteRecords(dataStore);
                    }

                }

            }
            else
            {
                // If there was an error report error
                responseMessage = error;
            }

            return new OkObjectResult(responseMessage);

        }

        public static decimal MPGtoKPL(decimal mpg) // Converts Miles per Gallon (Imp) to Km per Litre
        {
            decimal conversionFactor = 2.82481061M;
            decimal kpl = decimal.Divide(mpg, conversionFactor);
            return kpl;
        }

        public static decimal MPGtoL100(decimal mpg) // Converts Miles per Gallon (Imp) to Litres per 100km
        {
            decimal conversionFactor = 282.481061M;
            decimal l100 = decimal.Divide(conversionFactor, mpg);
            return l100;
        }

        public static decimal MilestoKM(decimal miles) // Converts Miles to Kilometers
        {
            decimal conversionFactor = 1.609344M;
            decimal kilometers = decimal.Multiply(miles, conversionFactor);
            return kilometers;
        }

        public class recordDesign //The design of the csv file
        {
            public DateTime date { get; set; }
            public int calcDays { get; set; }
            public decimal carODO { get; set; }
            public decimal calcMiles { get; set; }
            public decimal calcKm { get; set; }
            public decimal calcKmD { get; set; }
            public decimal carFuel { get; set; }
            public decimal carMPG { get; set; }
            public decimal carKPL { get; set; }
            public decimal calcKPL { get; set; }
            public decimal varKPL { get; set; }
            public decimal carL100 { get; set; }
            public decimal calcL100 { get; set; }
            public decimal varL100 { get; set; }
        }

        public class requestDesign // The design for the data that's sent in the http request
        {
            public string key { get; set; }
            public string date { get; set; }
            public string odo { get; set; }
            public string carMPG { get; set; }
            public string fuel { get; set; }
        }
    }


}
