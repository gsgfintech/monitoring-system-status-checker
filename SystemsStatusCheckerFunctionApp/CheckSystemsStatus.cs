using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;
using System.Threading;
using Capital.GSG.FX.Monitoring.Server.Connector;
using System.Collections.Generic;
using Capital.GSG.FX.Data.Core.SystemData;
using Capital.GSG.FX.Utils.Core;
using Net.Teirlinck.SlackPoster;
using System.Linq;
using System.Text;

namespace SystemsStatusCheckerFunctionApp
{
    public static class CheckSystemsStatus
    {
        [FunctionName("CheckSystemsStatus")]
        public static void Run([TimerTrigger("0 */2 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info("Starting SystemsStatusChecker");

            RunAsync(log).Wait();

            log.Info("Exiting SystemsStatusChecker");
        }

        private static async Task RunAsync(TraceWriter logger)
        {
            string clientId = GetEnvironmentVariable("Monitoring:ClientId");
            string appKey = GetEnvironmentVariable("Monitoring:AppKey");

            // 1. Dev
            logger.Info("Checking DEV systems");

            string devBackendAddress = GetEnvironmentVariable("Monitoring:Dev:BackendAddress");
            string devBackendAppUri = GetEnvironmentVariable("Monitoring:Dev:BackendAppIdUri");

            BackendSystemStatusesConnector devConnector = (new LightMonitoringServerConnector(clientId, appKey, devBackendAddress, devBackendAppUri)).SystemStatusesConnector;

            await DoCheckAsync(logger, devConnector);

            // 2. QA
            logger.Info("Checking QA systems");

            string qaBackendAddress = GetEnvironmentVariable("Monitoring:QA:BackendAddress");
            string qaBackendAppUri = GetEnvironmentVariable("Monitoring:QA:BackendAppIdUri");

            BackendSystemStatusesConnector qaConnector = (new LightMonitoringServerConnector(clientId, appKey, qaBackendAddress, qaBackendAppUri)).SystemStatusesConnector;

            await DoCheckAsync(logger, devConnector);

            // 3. Prod
            logger.Info("Checking PROD systems");

            string prodBackendAddress = GetEnvironmentVariable("Monitoring:Prod:BackendAddress");
            string prodBackendAppUri = GetEnvironmentVariable("Monitoring:Prod:BackendAppIdUri");

            BackendSystemStatusesConnector prodConnector = (new LightMonitoringServerConnector(clientId, appKey, prodBackendAddress, prodBackendAppUri)).SystemStatusesConnector;

            await DoCheckAsync(logger, prodConnector);
        }

        private static async Task DoCheckAsync(TraceWriter logger, BackendSystemStatusesConnector prodConnector)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            List<SystemStatus> systems = await prodConnector.GetAll(cts.Token);

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

                        await prodConnector.AddOrUpdate(system, cts2.Token);
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
    }
}