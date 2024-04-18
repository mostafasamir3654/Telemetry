using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

var meter = new Meter("MyApplication");

var counter = meter.CreateCounter<int>("Requests");
var histogram = meter.CreateHistogram<float>("RequestDuration", unit: "ms");
meter.CreateObservableGauge("ThreadCount", () => new[] { new Measurement<int>(ThreadPool.ThreadCount) });

var httpClient = new HttpClient();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var logger = app.Logger;

var activitySource = new ActivitySource("SampleActivitySource");

int RollDice()
{
    return Random.Shared.Next(1, 7);
}

async Task<string> HandleRollDiceAsync(string? player)
{
    var result = RollDice();

    if (string.IsNullOrEmpty(player))
    {
        logger?.LogInformation("Anonymous player is rolling the dice: {result}", result);
    }
    else
    {
        logger?.LogInformation("{player} is rolling the dice: {result}", player, result);
    }

    // The sampleActivity is automatically linked to the parent activity (the one from
    // ASP.NET Core in this case).
    // You can get the current activity using Activity.Current.
    using (var sampleActivity = activitySource?.StartActivity("Sample", ActivityKind.Server))
    {
        // note that "sampleActivity" can be null here if nobody listen events generated
        // by the "SampleActivitySource" activity source.
        sampleActivity?.AddTag("Name", player);
        sampleActivity?.AddBaggage("SampleContext", player);

        // Simulate a long running operation
        await Task.Delay(1000);
    }

    counter.Add(1, KeyValuePair.Create<string, object?>("name", player));
    var stopwatch = Stopwatch.StartNew();
    await httpClient.GetStringAsync("https://www.meziantou.net");

    // Measure the duration in ms of requests and includes the host in the tags
    histogram.Record(stopwatch.ElapsedMilliseconds,
        tag: KeyValuePair.Create<string, object?>("Host", "www.meziantou.net"));


    return result.ToString(CultureInfo.InvariantCulture);
}

app.MapGet("/rolldice/{player?}", HandleRollDiceAsync);


System.Console.WriteLine(System.Security.Principal.WindowsIdentity.GetCurrent().Name);
app.Run();