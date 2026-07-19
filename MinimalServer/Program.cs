using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

class Program
{
    static async Task Main()
    {
        const string pkiPath = "pki";
        const bool autoAcceptUntrustedCertificates = true;

        if (Directory.Exists(pkiPath))
        {
            //Directory.Delete(pkiPath, true);
        }

        const string applicationName = "MinimalServer";

        var baseAddress = $"opc.tcp://localhost:4840/{applicationName}";

        var telemetry = DefaultTelemetry.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        var applicationConfiguration = new ApplicationConfiguration(telemetry)
        {
            ApplicationName = applicationName,
            ApplicationType = ApplicationType.Server,
            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = { baseAddress },
                SecurityPolicies =
                {
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.SignAndEncrypt,
                        SecurityPolicyUri = SecurityPolicies.Basic256Sha256
                    }
                },
                UserTokenPolicies = { new UserTokenPolicy(UserTokenType.Anonymous) },
                MaxRegistrationInterval = 0
            },
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
            },
            TransportQuotas = new TransportQuotas()
        };

        await applicationConfiguration.ValidateAsync(ApplicationType.Server);

        var applicationInstance = new ApplicationInstance(applicationConfiguration, telemetry);
        await applicationInstance.CheckApplicationInstanceCertificatesAsync(false);

        var server = new MinimalServer();

        await applicationInstance.StartAsync(server);

        Console.WriteLine($"Server running at {baseAddress}");
        Console.ReadKey();
        await server.StopAsync();
    }
}

class MinimalServer : StandardServer
{
    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        return new MasterNodeManager(server, configuration, null, new MyNodeManager(server, configuration));
    }
}

class MyNodeManager : CustomNodeManager2
{
    public MyNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : base(server, configuration, "http://example.com/OpcUaServer")
    {
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        base.CreateAddressSpace(externalReferences);

        var device = new BaseObjectState(null)
        {
            NodeId = new NodeId("MyDevice", NamespaceIndex),
            BrowseName = new QualifiedName("MyDevice", NamespaceIndex),
            DisplayName = "My Device"
        };

        var temperature = new BaseDataVariableState(null)
        {
            NodeId = new NodeId("MyDevice.Temperature", NamespaceIndex),
            BrowseName = new QualifiedName("Temperature", NamespaceIndex),
            DisplayName = "Temperature",
            DataType = DataTypeIds.Double,
            Value = 21.5
        };

        device.AddChild(temperature);

        AddPredefinedNode(SystemContext, device);

        externalReferences[ObjectIds.ObjectsFolder] =
        [
            new NodeStateReference(ReferenceTypeIds.Organizes, false, device.NodeId)
        ];
    }
}