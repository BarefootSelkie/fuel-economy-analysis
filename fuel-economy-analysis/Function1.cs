using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.VisualBasic;

namespace fuel_economy_analysis
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string error = "Error: 400";
            bool failed = false;
            string name = req.Query["name"];
            string dateIn = req.Query["date"];
            string odoIn = req.Query["odo"];
            string carMPGIn = req.Query["carMPG"];
            string fuelIn = req.Query["fuel"];


            // Get data from the request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
            dateIn = dateIn ?? data?.dateIn;
            odoIn = odoIn ?? data?.odoIn;
            carMPGIn = carMPGIn ?? data?.carMPGIn;
            fuelIn = fuelIn ?? data?.fuelIn;

            // Convert data from strings and checks data is okay

            DateTime date;
            decimal odo, carMPG, fuel;

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

            if (failed)
            {
                responseMessage = error;
            }
            else
            {
                responseMessage = $"Got data, returning {name}, Date: {dateIn}, ODO: {odo}, CarMPG: {carMPG}, Fuel: {fuel}";
            }

            return new OkObjectResult(responseMessage);
        }
    }
}
