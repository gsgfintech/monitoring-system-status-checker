using Capital.GSG.FX.Data.Core.SystemData;
using Capital.GSG.FX.Monitoring.Server.Connector;
using Capital.GSG.FX.Utils.Core;
using log4net;
using Net.Teirlinck.SlackPoster;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

private static TraceWriter logger;

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    logger = log;

    logger.Info("Starting SystemsStatusChecker");

    RunAsync().Wait();

    logger.Info("Exiting SystemsStatusChecker");
}

private static async Task RunAsync()
{
    string clientId = GetEnvironmentVariable("Monitoring:ClientId");
    string appKey = GetEnvironmentVariable("Monitoring:AppKey");

    string backendAddress = GetEnvironmentVariable("Monitoring:BackendAddress");
    string backendAppUri = GetEnvironmentVariable("Monitoring:BackendAppIdUri");

    BackendSystemStatusesConnector connector = (new LightMonitoringServerConnector(clientId, appKey, backendAddress, backendAppUri)).SystemStatusesConnector;

    CancellationTokenSource cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromSeconds(30));

    List<SystemStatus> systems = await connector.GetAll(cts.Token);

    if (!systems.IsNullOrEmpty())
    {
        logger.Info($"Processing {systems.Count} systems from DB");

        SimpleSlackPoster slackPoster = new SimpleSlackPoster(GetEnvironmentVariable("SlackWebHook"));

        foreach (var system in systems)
        {
            bool needToUpdate = false;

            if (system.IsAlive) // skip systems that are stopped
            {
                // Mark systems for which LastHeardFrom is more than 2 minutes ago with status STOPPED
                if (DateTimeOffset.Now.Subtract(system.LastHeardFrom.ToLocalTime()) > TimeSpan.FromMinutes(5))
                {
                    if (system.IsAlive)
                    {
                        logger.Info($"Last status update for {system.Name} was received at {system.LastHeardFrom.ToLocalTime()}, more than 5 minutes ago. Marking this system as STOPPED");
                        system.IsAlive = false;

                        needToUpdate = true;
                    }
                }

                // Check for any ack expiration
                var ackedAttributes = system.Attributes?.Where(attr => attr.AckedUntil.HasValue)?.ToArray();

                if (!ackedAttributes.IsNullOrEmpty())
                {
                    foreach (var attribute in ackedAttributes)
                    {
                        if (attribute.Level == SystemStatusLevel.GREEN || DateTimeOffset.Now > attribute.AckedUntil.Value)
                        {
                            logger.Info($"Ack of {system.Name}.{attribute.Name} is expired ({attribute.AckedUntil.Value}) or no longer needed. Removing the ack");

                            attribute.AckedUntil = null;

                            needToUpdate = true;
                        }
                    }
                }

                // Check if we should send alerts for red attributes
                var attributesToAlert = system.Attributes?.Where(attr => attr.Level == SystemStatusLevel.RED && !attr.AckedUntil.HasValue);

                if (!attributesToAlert.IsNullOrEmpty())
                {
                    foreach (var attribute in attributesToAlert)
                    {
                        string monitoringEndpoint = "https://fxmonitor.gsg.capital:9098";

                        StringBuilder body = new StringBuilder($"{attribute.Name}: {attribute.Value}\n");
                        body.Append($"\nAck for <{monitoringEndpoint}/api/systemsstatusweb/{system.Name}/{attribute.Name}/ack/30m|30 minutes>, ");
                        body.Append($"<{monitoringEndpoint}/api/systemsstatusweb/{system.Name}/{attribute.Name}/ack/1h|1 hour>, ");
                        body.Append($"<{monitoringEndpoint}/api/systemsstatusweb/{system.Name}/{attribute.Name}/ack/4h|4 hours>, ");
                        body.Append($"<{monitoringEndpoint}/api/systemsstatusweb/{system.Name}/{attribute.Name}/ack/12h|12 hours>, ");
                        body.Append($"<{monitoringEndpoint}/api/systemsstatusweb/{system.Name}/{attribute.Name}/ack/1d|1 day>, ");
                        body.Append($"<{monitoringEndpoint}/api/systemsstatusweb/{system.Name}/{attribute.Name}/ack/1w|1 week>");

                        CancellationTokenSource cts3 = new CancellationTokenSource();
                        cts3.CancelAfter(TimeSpan.FromSeconds(10));

                        await slackPoster.PostAlert(SlackAlertLevel.ALERT, system.Name, "RED Status Attribute", body.ToString(), ct: cts.Token);
                    }
                }
            }
            else
            {
                // Clear any remaining attribute
                if (!system.Attributes.IsNullOrEmpty())
                {
                    logger.Info($"Removing {system.Attributes.Count} existing attributes for system {system.Name} from the database");
                    system.Attributes = null;

                    needToUpdate = true;
                }
            }

            if (needToUpdate)
            {
                logger.Info($"Updating system {system.Name} status in DB");

                CancellationTokenSource cts2 = new CancellationTokenSource();
                cts2.CancelAfter(TimeSpan.FromSeconds(10));

                await connector.AddOrUpdate(system, cts2.Token);
            }
        }
    }
    else
        logger.Error("Failed to load systems status list");
}

private static string GetEnvironmentVariable(string name)
{
    return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}