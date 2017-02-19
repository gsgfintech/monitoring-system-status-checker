using Capital.GSG.FX.Utils.Core.Logging;
using log4net.Config;
using Microsoft.Extensions.Logging;

namespace SystemsStatusCheckerConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            GSGLoggerFactory.Instance.AddConsole();

            CheckSystemsStatus.Function.Run();
        }
    }
}
