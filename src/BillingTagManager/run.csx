using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using System.Threading; 
using System.Text;
using System.Linq; 
using System.Text.RegularExpressions;

public static async Task Run([TimerTrigger("%TimerTriggerInterval%")]TimerInfo myTimer, ILogger log)
{
    try 
    {
        AzureCredentials credentials = SdkContext.AzureCredentialsFactory.FromMSI(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud);
        var azure = Azure
            .Configure()
            .Authenticate(credentials)
            .WithDefaultSubscription();
        
        var periods = System.Environment.GetEnvironmentVariable("BillingPeriod", EnvironmentVariableTarget.Process);

        foreach (var period in periods.Split(';'))
        {
            log.LogInformation($"Get Azure Usage for period : {period}");
            var timePeriod = CalculateTimePeriod(period);
            if (timePeriod != null) 
            {
                log.LogInformation(JsonConvert.SerializeObject(timePeriod));
                var reports = await GetBillingUsage(azure.SubscriptionId, timePeriod, log);
                await UpdateResourceGroups(azure, period, reports, log);
            }
        }
    }
    catch (Exception ex) 
    {
        log.LogError(ex.ToString());
    }
}

private static async Task<List<CostReport>> GetBillingUsage(string subscriptionId, TimePeriod period, ILogger log) 
{

    var tokenProvider = new AzureServiceTokenProvider();
    var accessToken = await tokenProvider.GetAccessTokenAsync("https://management.azure.com/");

    var scope = $"/subscriptions/{subscriptionId}";
    var apiVersion = "2019-10-01";
    var url = $"https://management.azure.com/{scope}/providers/Microsoft.CostManagement/query?api-version={apiVersion}"; 

    var requestObject = new CostManagementUsageRequest() {
        Type = "Usage",
        TimeFrame = "Custom",
        TimePeriod = period,
        Dataset = new Dataset() {
            Aggregation = new Aggregation() {
                TotalCost = new TotalCost() {
                    Name = "PreTaxCost",
                    Function = "Sum"
                }
            },
            Grouping = new List<Grouping>() {
                new Grouping() {
                    Type = "Dimension",
                    Name = "ResourceGroup"
                }
            }
        }
    };

    var requestJson = JsonConvert.SerializeObject(requestObject);
    var reports = new List<CostReport>();

    using (var client = new HttpClient())
    {
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
        using (var request = new HttpRequestMessage(HttpMethod.Post, url))
        {
            var json = JsonConvert.SerializeObject(requestObject);
            using (var stringContent = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                request.Content = stringContent;

                using (var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None)
                    .ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    var responseObject = await response.Content.ReadAsAsync<CostManagementUsageResponse>();
                    reports.AddRange(responseObject.Properties.Rows.Select(r => new CostReport() {
                        ResourceGroupName = r[1],
                        Value = Math.Round(double.Parse(r[0]), 2).ToString(),
                        Currency = r[2]
                    }));

                    log.LogInformation(JsonConvert.SerializeObject(reports));
                }
            }
        }
    }

    return reports; 
}

private static async Task UpdateResourceGroups(IAzure azure, string period, List<CostReport> reports, ILogger log)
{
    foreach (var report in reports)
    {
        if (Regex.IsMatch(report.ResourceGroupName, @"^[-\w\._\(\)]+$", RegexOptions.None))
        {
            if (azure.ResourceGroups.CheckExistence(report.ResourceGroupName))
            {
                azure.ResourceGroups.GetByName(report.ResourceGroupName).Update().WithTag($"Billing-Tag-{period}", $"{report.Value} {report.Currency}").Apply();
            }
            else 
            {
                log.LogWarning($"Resource group {report.ResourceGroupName} not found");
            }
        }
    }
}

private static TimePeriod CalculateTimePeriod(string period)
{
    TimePeriod result = null; 
    var currentDate = DateTime.UtcNow; 

    var last = period.ToLower().Last(); 
    switch (last)
    {
        case 'h':
            {
                return new TimePeriod() {
                    From = currentDate.AddHours(-int.Parse(period.Substring(0, period.Length -1))),
                    To = currentDate
                };
            }
        case 'd':
            {
                return new TimePeriod() {
                    From = currentDate.AddDays(-int.Parse(period.Substring(0, period.Length -1))),
                    To = currentDate
                };
            }
    }
    return result; 
}

public class CostManagementUsageRequest
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("timeframe")]
    public string TimeFrame { get; set; }

    [JsonProperty("timePeriod")]
    public TimePeriod TimePeriod { get; set; }

    [JsonProperty("dataset")]
    public Dataset Dataset { get; set; }
}

public class Dataset
{
    [JsonProperty("aggregation")]
    public Aggregation Aggregation { get; set; }

    [JsonProperty("grouping")]
    public List<Grouping> Grouping { get; set; }
}

public class Aggregation
{
    [JsonProperty("totalCost")]
    public TotalCost TotalCost { get; set; }
}

public class TotalCost
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("function")]
    public string Function { get; set; }
}

public partial class Grouping
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}

public partial class TimePeriod
{
    [JsonProperty("from")]
    public DateTimeOffset From { get; set; }

    [JsonProperty("to")]
    public DateTimeOffset To { get; set; }
}

public class CostManagementUsageResponse
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public Guid Name { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("location")]
    public object Location { get; set; }

    [JsonProperty("sku")]
    public object Sku { get; set; }

    [JsonProperty("eTag")]
    public object ETag { get; set; }

    [JsonProperty("properties")]
    public Properties Properties { get; set; }
}

public class Properties
{
    [JsonProperty("nextLink")]
    public object NextLink { get; set; }

    [JsonProperty("columns")]
    public List<Column> Columns { get; set; }

    [JsonProperty("rows")]
    public List<List<string>> Rows { get; set; }
}

public class Column
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }
}

public class CostReport 
{
    public string ResourceGroupName { get; set; }
    public string Value { get; set; }
    public string Currency { get; set; }
}
