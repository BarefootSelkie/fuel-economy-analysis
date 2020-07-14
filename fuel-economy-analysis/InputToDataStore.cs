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
using Newtonsoft.Json;
using Microsoft.VisualBasic;
using CsvHelper;
using System.Globalization;
using System.Linq;

namespace fuel_economy_analysis
{
    public static class InputToDataStore
    {
        [FunctionName("InputToDataStore")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Blob("azure-webjobs-hosts/fueldata.csv", FileAccess.Read, Connection = "")] Stream myBlob,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");


            string error = "Error: 400";
            bool failed = false;
            string key = req.Query["key"];
            string secKey = "Change";
            string dateIn = req.Query["date"];
            string odoIn = req.Query["odo"];
            string carMPGIn = req.Query["carMPG"];
            string fuelIn = req.Query["fuel"];


            // Get data from the request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            key = key ?? data?.key;
            dateIn = dateIn ?? data?.dateIn;
            odoIn = odoIn ?? data?.odoIn;
            carMPGIn = carMPGIn ?? data?.carMPGIn;
            fuelIn = fuelIn ?? data?.fuelIn;

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
                using (StreamReader reader = new StreamReader(myBlob))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    // Load the csv file into a variable

                    var records = csv.GetRecords<datafile>();

                    // Do the calcuations

                    decimal workMiles = odo - records.Last().carODO;
                    decimal workKM = MilestoKM(workMiles);
                    decimal workCarKPL = MPGtoKPL(carMPG);
                    decimal workCarL100 = MPGtoL100(carMPG);
                    decimal workCalcKPL = workKM / fuel;
                    decimal workCalcL100 = (fuel / workKM) * 100M;

                    // Return recieved data and calcuated data for debugging

                    responseMessage = "Got data:" + System.Environment.NewLine +
                        $"Date: {dateIn}" + System.Environment.NewLine +
                        $"ODO: {odo}" + System.Environment.NewLine +
                        $"Car MPG: {carMPG}" + System.Environment.NewLine +
                        $"Fuel: {fuel}" + System.Environment.NewLine +
                        "Calcuated data:" + System.Environment.NewLine +
                        $"Car Miles Traveled: {workMiles}" + System.Environment.NewLine +
                        $"Car KM Traveled: {workKM}" + System.Environment.NewLine +
                        $"Car KPL: {workCarKPL}" + System.Environment.NewLine +
                        $"Car l/100km: {workCarL100}" + System.Environment.NewLine +
                        $"Actual KPL: {workCalcKPL}" + System.Environment.NewLine +
                        $"Actual l/100km: {workCalcL100}";

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

        public class datafile //The design of the csv file
        {
            public DateAndTime date { get; set; }
            public decimal carODO { get; set; }
            public decimal calcMiles { get; set; }
            public decimal calcKm { get; set; }
            public decimal carFuel { get; set; }
            public decimal carMPG { get; set; }
            public decimal carKPL { get; set; }
            public decimal carL100 { get; set; }
            public decimal calcKPL { get; set; }
            public decimal calcL100 { get; set; }
        }

    }


}
