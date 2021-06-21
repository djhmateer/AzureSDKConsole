using System;
using System.Collections.Generic;
using System.Diagnostics;
using AzureSDKConsole;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Network.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;



// source https://github.com/Azure-Samples/compute-dotnet-create-virtual-machines-from-generalized-image-or-specialized-vhd


var userName = "dave";
//var password = SdkContext.RandomResourceName("Pa5$", 15);
var password = "lAADDDA22!!";

List<string> ApacheInstallScriptUris = new() { "https://raw.githubusercontent.com/Azure/azure-libraries-for-net/master/Samples/Asset/install_apache.sh" };
var apacheInstallCommand = "bash install_apache.sh";

// Authenticate
//var credentials = SdkContext.AzureCredentialsFactory.FromFile(Environment.GetEnvironmentVariable("AZURE_AUTH_LOCATION"));
var credentials = SdkContext.AzureCredentialsFactory.FromFile(@"c:\temp\my.azureauth");

var sw = Stopwatch.StartNew();
var azure = Azure
    .Configure()
    .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
    .Authenticate(credentials)
    .WithDefaultSubscription();

Console.WriteLine("Selected subscription: " + azure.SubscriptionId);

//string rgName = SdkContext.RandomResourceName("AzureSDKTEST", 10);
var unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
string rgName = $"azure{unixTimestamp}";

//string linuxVmName1 = SdkContext.RandomResourceName("VM1", 10);
string linuxVmName1 = "vm";

//string publicIpDnsLabel = SdkContext.RandomResourceName("azuresdktest", 10);
string publicIpDnsLabel = rgName;

try
{
    // resource group
    Console.WriteLine($"creating rg {rgName}");
    var resourceGroup = azure.ResourceGroups
        .Define(rgName)
        .WithRegion(Region.EuropeWest)
        .Create();

    // network - vnet
    Console.WriteLine("creating network - vnet");
    var network = azure.Networks.Define("mynetwork")
        .WithRegion(Region.EuropeWest)
        //.WithNewResourceGroup()
        .WithExistingResourceGroup(resourceGroup)
        .WithAddressSpace("10.0.0.0/28")
        .WithSubnet("Front-end", "10.0.0.0/29")
        //.WithSubnet("subnet2", "10.0.0.8/29")
        .Create();

    // nsg
    Console.WriteLine("creating nsg and 2 rules");
    var frontEndNSG = azure.NetworkSecurityGroups.Define("nsg")
        .WithRegion(Region.EuropeWest)
        //.WithNewResourceGroup(rgName)
        .WithExistingResourceGroup(resourceGroup)
        .DefineRule("ALLOW-SSH")
            .AllowInbound()
            .FromAnyAddress()
            .FromAnyPort()
            .ToAnyAddress()
            .ToPort(22)
            .WithProtocol(SecurityRuleProtocol.Tcp)
            .WithPriority(100)
            .WithDescription("Allow SSH")
            .Attach()
        .DefineRule("ALLOW-HTTP")
            .AllowInbound()
            .FromAnyAddress()
            .FromAnyPort()
            .ToAnyAddress()
            .ToPort(80)
            .WithProtocol(SecurityRuleProtocol.Tcp)
            .WithPriority(101)
            .WithDescription("Allow HTTP")
            .Attach()
        .Create();

    // nic
    Console.WriteLine("creating nic");
    var networkInterface1 = azure.NetworkInterfaces.Define("nic")
        .WithRegion(Region.EuropeWest)
        //.WithExistingResourceGroup(rgName)
        .WithExistingResourceGroup(resourceGroup)
        .WithExistingPrimaryNetwork(network)
        .WithSubnet("Front-end")
        .WithPrimaryPrivateIPAddressDynamic()
        .WithNewPrimaryPublicIPAddress(publicIpDnsLabel)
        .WithIPForwarding()
        .WithExistingNetworkSecurityGroup(frontEndNSG)
        .Create();


    // vm
    Console.WriteLine("Creating a Linux VM");
    var linuxVM = azure.VirtualMachines.Define(linuxVmName1)
            .WithRegion(Region.EuropeWest)
            //.WithNewResourceGroup(rgName)
            .WithExistingResourceGroup(resourceGroup)
            //.WithNewPrimaryNetwork("10.0.0.0/28")
            //.WithPrimaryPrivateIPAddressDynamic()
            //.WithNewPrimaryPublicIPAddress(publicIpDnsLabel)
            .WithExistingPrimaryNetworkInterface(networkInterface1)
            // Nice strongly typed image names - but old
            //.WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
            // az vm image list --publisher Canonical --sku 20_04-lts-gen2 --output table --all
            //.WithLatestLinuxImage("Canonical", "0001-com-ubuntu-server-focal", "20_04-lts-gen2")
            //.WithLatestLinuxImage("Canonical", "0001-com-ubuntu-server-focal", "20_04-lts")
            .WithLatestLinuxImage("Canonical", "0001-com-ubuntu-server-focal", "20_04-lts")
            .WithRootUsername(userName)
            .WithRootPassword(password)
            // look into this need pem format
            //.WithSsh()
            // **look into this **
            //.WithUnmanagedDisks()
            // Nice strongly typed machine size - but need a crib sheet for costs
            // https://azure.microsoft.com/en-gb/pricing/details/virtual-machines/linux/
            //.WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
            // £5 per month
            .WithSize(VirtualMachineSizeTypes.StandardB1ms)
            

            .DefineNewExtension("CustomScriptForLinux")
                .WithPublisher("Microsoft.OSTCExtensions")
                .WithType("CustomScriptForLinux")
                .WithVersion("1.4")
                .WithMinorVersionAutoUpgrade()
                // pulling a bash script from a Uri
                .WithPublicSetting("fileUris", ApacheInstallScriptUris)
                // sending the bash script
                .WithPublicSetting("commandToExecute", apacheInstallCommand)
                .Attach()
            .Create();

    Console.WriteLine("Created a Linux VM: " + linuxVM.Id);
    Utilities.PrintVirtualMachine(linuxVM);
    Console.WriteLine();

    var conn = $"ssh -o StrictHostKeyChecking=no dave@{rgName}.westeurope.cloudapp.azure.com";
    Console.WriteLine(conn);
    Console.WriteLine(password);
    
    //// De-provision the virtual machine
    //Utilities.DeprovisionAgentInLinuxVM(linuxVM.GetPrimaryPublicIPAddress().Fqdn, 22, UserName, Password);

    ////=============================================================
    //// Deallocate the virtual machine
    //Console.WriteLine("Deallocate VM: " + linuxVM.Id);

    //linuxVM.Deallocate();

    //Console.WriteLine("Deallocated VM: " + linuxVM.Id + "; state = " + linuxVM.PowerState);

    ////=============================================================
    //// Generalize the virtual machine
    //Console.WriteLine("Generalize VM: " + linuxVM.Id);

    //linuxVM.Generalize();

    //Console.WriteLine("Generalized VM: " + linuxVM.Id);

    ////=============================================================
    //// Capture the virtual machine to get a 'Generalized image' with Apache
    //Console.WriteLine("Capturing VM: " + linuxVM.Id);

    //var capturedResultJson = linuxVM.Capture("capturedvhds", "img", true);

    //Console.WriteLine("Captured VM: " + linuxVM.Id);

    ////=============================================================
    //// Create a Linux VM using captured image (Generalized image)
    //JObject o = JObject.Parse(capturedResultJson);
    //JToken resourceToken = o.SelectToken("$.resources[?(@.properties.storageProfile.osDisk.image.uri != null)]");
    //if (resourceToken == null)
    //{
    //    throw new Exception("Could not locate image uri under expected section in the capture result -" + capturedResultJson);
    //}
    //string capturedImageUri = (string)(resourceToken["properties"]["storageProfile"]["osDisk"]["image"]["uri"]);

    //Console.WriteLine("Creating a Linux VM using captured image - " + capturedImageUri);

    //var linuxVM2 = azure.VirtualMachines.Define(linuxVmName2)
    //        .WithRegion(Region.USWest)
    //        .WithExistingResourceGroup(rgName)
    //        .WithNewPrimaryNetwork("10.0.0.0/28")
    //        .WithPrimaryPrivateIPAddressDynamic()
    //        .WithoutPrimaryPublicIPAddress()
    //        .WithStoredLinuxImage(capturedImageUri) // Note: A Generalized Image can also be an uploaded VHD prepared from an on-premise generalized VM.
    //        .WithRootUsername(UserName)
    //        .WithRootPassword(Password)
    //        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
    //        .Create();

    //Utilities.PrintVirtualMachine(linuxVM2);

    //var specializedVhd = linuxVM2.OSUnmanagedDiskVhdUri;
    ////=============================================================
    //// Deleting the virtual machine
    //Console.WriteLine("Deleting VM: " + linuxVM2.Id);

    //azure.VirtualMachines.DeleteById(linuxVM2.Id); // VM required to be deleted to be able to attach it's
    //                                               // OS Disk VHD to another VM (Deallocate is not sufficient)

    //Console.WriteLine("Deleted VM");

    ////=============================================================
    //// Create a Linux VM using 'specialized VHD' of previous VM

    //Console.WriteLine("Creating a new Linux VM by attaching OS Disk vhd - "
    //        + specializedVhd
    //        + " of deleted VM");

    //var linuxVM3 = azure.VirtualMachines.Define(linuxVmName3)
    //        .WithRegion(Region.USWest)
    //        .WithExistingResourceGroup(rgName)
    //        .WithNewPrimaryNetwork("10.0.0.0/28")
    //        .WithPrimaryPrivateIPAddressDynamic()
    //        .WithoutPrimaryPublicIPAddress()
    //        .WithSpecializedOSUnmanagedDisk(specializedVhd, OperatingSystemTypes.Linux) // New user credentials cannot be specified
    //        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))         // when attaching a specialized VHD
    //        .Create();

    //Utilities.PrintVirtualMachine(linuxVM3);
}
finally
{
    try
    {
        Console.WriteLine("Press any key to delete the rg");
        Console.ReadLine();
        Console.WriteLine("Deleting rg");
        //Console.WriteLine("Deleting Resource Group: " + rgName);
        azure.ResourceGroups.DeleteByName(rgName);
        //Console.WriteLine("Deleted Resource Group: " + rgName);
    }
    catch
    {
        Console.WriteLine("Did not create any resources in Azure. No clean up is necessary");
    }
}

Console.WriteLine($"done in {sw.ElapsedMilliseconds}ms");

