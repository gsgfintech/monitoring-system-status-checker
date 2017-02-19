#SystemsStatusCheckerConsoleApp

Do not commit the AppKey to source control. 

Configuration variables and parameters can be passed by creating a local app.config in the SystemsStatusCheckerConsoleApp directory

The app.config should not be added to source control

Eg:

App.config

<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <configSections>
    <section name="log4net" type="log4net.Config.Log4NetConfigurationSectionHandler, log4net" />
  </configSections>
  <appSettings>
    <add key="Monitoring:ClientId" value="9655ef89-e3d5-438e-8226-724f7a9986dc" />
    <add key="Monitoring:AppKey" value="<TODO>" />
    <add key="Monitoring:BackendAddress" value="https://stratedgeme-monitor-backend.azurewebsites.net" />
    <add key="Monitoring:BackendAppIdUri" value="https://gsgfintech.com/stratedgeme-monitor-backend-new" />
  </appSettings>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.6" />
  </startup>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="System.Xml.ReaderWriter" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.1.0.0" newVersion="4.1.0.0" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="System.Diagnostics.DiagnosticSource" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.0.1.0" newVersion="4.0.1.0" />
      </dependentAssembly>
      <dependentAssembly>
        <assemblyIdentity name="System.Net.Http" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-4.1.1.0" newVersion="4.1.1.0" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
  <log4net>
    <appender name="ConsoleAppender" type="log4net.Appender.ConsoleAppender">
      <layout type="log4net.Layout.PatternLayout">
        <conversionPattern value="%date [%thread] %-5level %logger - %message%newline" />
      </layout>
    </appender>
    <appender name="TraceAppender" type="log4net.Appender.TraceAppender">
      <layout type="log4net.Layout.PatternLayout">
        <conversionPattern value="%date [%thread] %-5level %logger [%property{NDC}] - %message%newline" />
      </layout>
    </appender>
    <root>
      <level value="DEBUG" />
      <appender-ref ref="ConsoleAppender" />
      <appender-ref ref="TraceAppender" />
    </root>
  </log4net>
</configuration>

#SystemsStatusCheckerFunctionApp

Do not commit the AppKey to source control.

Configuration variables and parameters can be passed by creating a local appsettings.json in the same directory as the host.json

The appsettings.json should not be added to source control

Eg:

appsettings.json:

{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true;",
    "AzureWebJobsDashboard": "",
    "Monitoring:ClientId": "",
    "Monitoring:AppKey": "",
    "Monitoring:BackendAddress": "",
    "Monitoring:BackendAppIdUri": "",
  }
}
 