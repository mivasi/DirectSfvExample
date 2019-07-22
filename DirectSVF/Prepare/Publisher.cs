using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;

namespace Prepare
{
    /// <summary>
    /// Constants.
    /// </summary>
    internal class Constants
    {
        /// <summary>
        /// Input the Inventor IO engine version, you can set it to 22,23 and 24.
        /// </summary>
        private const int EngineVersion = 24;

        public static readonly string Engine = $"Autodesk.Inventor+{EngineVersion}";

        /// <summary>
        /// Description of the sample SVF.  
        /// </summary>
        public const string Description = "SVF export from Inventor IPT or IAM.";

        internal static class Bundle
        {
            /// <summary>
            /// Define the app bundle id. 
            /// </summary>
            public static readonly string Id = "ExportToSvf";

            /// <summary>
            /// Define the app bundle nick name. 
            /// </summary>
            public const string Label = "alpha";

            public static readonly AppBundle Definition = new AppBundle
                                                              {
                                                                  Engine = Engine,
                                                                  Id = Id,
                                                                  Description = Description
                                                              };
        }

        internal static class Activity
        {
            public static readonly string Id = Bundle.Id;
            public const string Label = Bundle.Label;
        }

        internal static class Parameters
        {
            public const string InventorDoc = nameof(InventorDoc);
            public const string OutputZip = nameof(OutputZip);
        }
    }

    internal class Publisher
    {
        private string _nickname;
        internal IConfiguration Configuration;
        internal DesignAutomationClient Client { get; }
        private static string PackagePathname { get; set; }

        /// <summary>
        /// Get command line for activity.
        /// </summary>
        private static List<string> GetActivityCommandLine()
        {
            return new List<string> { $"$(engine.path)\\InventorCoreConsole.exe /al $(appbundles[{Constants.Activity.Id}].path) /i $(args[{Constants.Parameters.InventorDoc}].path)" };
        }

        /// <summary>
        /// Get activity parameters.
        /// </summary>
        private static Dictionary<string, Parameter> GetActivityParams()
        {
            return new Dictionary<string, Parameter>
                       {
                           {
                               Constants.Parameters.InventorDoc,
                               new Parameter
                                   {
                                       Verb = Verb.Get,
                                       Description = "IPT file or ZIP with assembly to process",
                                   }
                           },
                           {
                               Constants.Parameters.OutputZip,
                               new Parameter
                                   {
                                       Verb = Verb.Put,
                                       LocalName = "SvfOutput",
                                       Description = "Resulting files with SVF",
                                       Zip = true
                                   }
                           }
                       };
        }


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration"></param>
        public Publisher(IConfiguration configuration)
        {
            Configuration = configuration;
            Client = CreateDesignAutomationClient();
            PackagePathname = configuration.GetValue<string>("PackagePathname");
        }

        public async Task ReparingDriectSvf()
        {
            await this.PostAppBundleAsync();
            await this.PublishActivityAsync();
            Console.WriteLine("Next >>> Please active viewer project and use forge viewer to show the SVF result!");
        }

        public async Task PostAppBundleAsync()
        {
            if (!File.Exists(PackagePathname))
                throw new Exception("App Bundle with package is not found. Ensure it set correctly in appsettings.json");

            var shortAppBundleId = $"{Constants.Bundle.Id}+{Constants.Bundle.Label}";
            Console.WriteLine($"Posting app bundle '{shortAppBundleId}'.");

            // try to remove the already existing bundle
            var response = await Client.AppBundlesApi.GetAppBundleAsync(shortAppBundleId, throwOnError: false);
            if (response.HttpResponse.StatusCode == HttpStatusCode.OK)
            {
                await Client.AppBundlesApi.DeleteAppBundleAsync(Constants.Bundle.Id);
                Console.WriteLine("Removed the existed app bundle.");
            }

            // Create new app bundle
            Console.WriteLine($"Creating app bundle '{Constants.Bundle.Id}'");
            await Client.CreateAppBundleAsync(Constants.Bundle.Definition, Constants.Bundle.Label, PackagePathname);
            Console.WriteLine("The app bundle is created.");
        }

        private async Task<string> GetFullActivityId()
        {
            string nickname = await GetNicknameAsync();
            return $"{nickname}.{Constants.Activity.Id}+{Constants.Activity.Label}";
        }

        public async Task<string> GetNicknameAsync()
        {
            if (_nickname == null)
            {
                _nickname = await Client.GetNicknameAsync("me");
            }

            return _nickname;
        }

        public async Task PublishActivityAsync()
        {
            var nickname = await GetNicknameAsync();

            // prepare activity definition
            var activity = new Activity
            {
                Appbundles = new List<string> { $"{nickname}.{Constants.Bundle.Id}+{Constants.Bundle.Label}" },
                Id = Constants.Activity.Id,
                Engine = Constants.Engine,
                Description = Constants.Description,
                CommandLine = GetActivityCommandLine(),
                Parameters = GetActivityParams()
            };

            // check if the activity exists already
            var response = await Client.ActivitiesApi.GetActivityAsync(await GetFullActivityId(), throwOnError: false);

            if (response.HttpResponse.StatusCode == HttpStatusCode.OK)
            {
                await Client.DeleteActivityAsync(Constants.Activity.Id);
                Console.WriteLine("Removed the existed activity.");
            }

            Console.WriteLine($"Creating activity '{Constants.Activity.Id}'");
            await Client.CreateActivityAsync(activity, Constants.Activity.Label);
            Console.WriteLine("The activity is created.");
        }

        private DesignAutomationClient CreateDesignAutomationClient()
        {
            var forgeService = CreateForgeService();

            var rsdkCfg = Configuration.GetSection("DesignAutomation").Get<Configuration>();
            var options = (rsdkCfg == null) ? null : Options.Create(rsdkCfg);
            return new DesignAutomationClient(forgeService, options);
        }

        private ForgeService CreateForgeService()
        {
            var forgeCfg = CreateForgeConfig();
            var httpMessageHandler = new ForgeHandler(Options.Create(forgeCfg))
            {
                InnerHandler = new HttpClientHandler()
            };

            return new ForgeService(new HttpClient(httpMessageHandler));
        }

        private ForgeConfiguration CreateForgeConfig()
        {
            return Configuration.GetSection("Forge").Get<ForgeConfiguration>();
        }


    }
}
