using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

class Program
{
    static async Task Main()
    {
        const string discoveryUrl = "opc.tcp://127.0.0.1:4840";

        const string pkiPath = "pki";
        const bool autoAcceptUntrustedCertificates = true;

        if (Directory.Exists(pkiPath))
        {
            //Directory.Delete(pkiPath, true);
        }

        const string applicationName = "MinimalClient";

        var telemetry = DefaultTelemetry.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        var applicationConfiguration = new ApplicationConfiguration(telemetry)
        {
            ApplicationName = applicationName,
            ApplicationType = ApplicationType.Client,
            ClientConfiguration = new ClientConfiguration(),
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = $"{pkiPath}/own",
                    SubjectName = $"CN={applicationName}"
                },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = CertificateStoreType.Directory, StorePath = $"{pkiPath}/trusted" },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = CertificateStoreType.Directory, StorePath = $"{pkiPath}/issuers" },
                RejectedCertificateStore = new CertificateTrustList { StoreType = CertificateStoreType.Directory, StorePath = $"{pkiPath}/rejected" },
                AutoAcceptUntrustedCertificates = autoAcceptUntrustedCertificates
            }
        };

        await applicationConfiguration.ValidateAsync(ApplicationType.Client);

        var applicationInstance = new ApplicationInstance(applicationConfiguration, telemetry);
        await applicationInstance.CheckApplicationInstanceCertificatesAsync(false);

        var endpoint = await CoreClientUtils.SelectEndpointAsync(applicationConfiguration, discoveryUrl, useSecurity: true, telemetry);

        var configuredEndpoint = new ConfiguredEndpoint(null, endpoint);

        var sessionFactory = new DefaultSessionFactory(telemetry);

        var session = await sessionFactory.CreateAsync(applicationConfiguration,
                                                       configuredEndpoint,
                                                       updateBeforeConnect: false,
                                                       checkDomain: true,
                                                       sessionName: "MinimalClient",
                                                       sessionTimeout: 60000,
                                                       identity: new UserIdentity(),
                                                       preferredLocales: null);

        Console.WriteLine("Connected");

        await session.CloseAsync();
    }
}