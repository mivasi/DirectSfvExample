using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Forge;
using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Forge.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Activity = Autodesk.Forge.DesignAutomation.Model.Activity;
using WorkItem = Autodesk.Forge.DesignAutomation.Model.WorkItem;

namespace Interaction
{
    internal partial class Publisher
    {
        private string _nickname;
        internal IConfiguration Configuration;
        internal DesignAutomationClient Client { get; }
        private static string PackagePathname { get; set; }

        internal string InputFileUrl;

        internal string OutputFileUrl;

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

        /// <summary>
        /// List available engines.
        /// </summary>
        public async Task ListEnginesAsync()
        {
            string page = null;
            do
            {
                using (var response = await Client.EnginesApi.GetEnginesAsync(page))
                {
                    if (!response.HttpResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Request failed");
                        break;
                    }

                    foreach (var engine in response.Content.Data)
                    {
                        Console.WriteLine(engine);
                    }

                    page = response.Content.PaginationToken;
                }
            } while (page != null);
        }

        public async Task PostAppBundleAsync()
        {
            if (!File.Exists(PackagePathname))
                throw new Exception("App Bundle with package is not found. Ensure it set correctly in appsettings.json");

            var shortAppBundleId = $"{Constants.Bundle.Id}+{Constants.Bundle.Label}";
            Console.WriteLine($"Posting app bundle '{shortAppBundleId}'.");

            // try to get already existing bundle
            var response = await Client.AppBundlesApi.GetAppBundleAsync(shortAppBundleId, throwOnError: false);
            if (response.HttpResponse.StatusCode == HttpStatusCode.NotFound) // create new bundle
            {
                await Client.CreateAppBundleAsync(Constants.Bundle.Definition, Constants.Bundle.Label, PackagePathname);
                Console.WriteLine("Created new app bundle.");
            }
            else // create new bundle version
            {
                var version = await Client.UpdateAppBundleAsync(Constants.Bundle.Definition, Constants.Bundle.Label, PackagePathname);
                Console.WriteLine($"Created version #{version} for '{shortAppBundleId}' app bundle.");
            }
        }

        public async Task RunWorkItemAsync()
        {
            await UploadInputFile();

            // create work item
            var wi = new WorkItem
            {
                ActivityId = await GetFullActivityId(),
                Arguments = GetWorkItemArgs()
            };

            // run WI and wait for completion
            var status = await Client.CreateWorkItemAsync(wi);
            Console.WriteLine($"Created WI {status.Id}");
            while (status.Status == Status.Pending || status.Status == Status.Inprogress)
            {
                Console.Write(".");
                Thread.Sleep(2000);
                status = await Client.GetWorkitemStatusAsync(status.Id);
            }

            Console.WriteLine();
            Console.WriteLine($"WI {status.Id} completed with {status.Status}");
            Console.WriteLine();

            // dump report
            var client = new HttpClient();
            var report = await client.GetStringAsync(status.ReportUrl);
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write(report);
            Console.ForegroundColor = oldColor;
            Console.WriteLine();
        }

        public async Task UploadInputFile()
        {
            Scope[] scope = new Scope[] { Scope.BucketCreate, Scope.BucketUpdate, Scope.DataRead, Scope.DataWrite };
            TwoLeggedApi twoLeggedApi = new TwoLeggedApi();
            var forgeConfig = CreateForgeConfig();
            Autodesk.Forge.Client.ApiResponse<dynamic> bearerResponse = await twoLeggedApi.AuthenticateAsyncWithHttpInfo(forgeConfig.ClientId, forgeConfig.ClientSecret, oAuthConstants.CLIENT_CREDENTIALS, scope);
            if (bearerResponse.StatusCode != 200)
            {
                throw new Exception("Request failed! (with HTTP response " + bearerResponse.StatusCode + ")");
            }

            var bearer = bearerResponse.Data;
            var bucketKey = forgeConfig.ClientId.ToLower();
            Autodesk.Forge.Client.Configuration.Default.AccessToken = bearer.access_token;
            var bucketsApi = new BucketsApi();
            var objectsApi = new ObjectsApi();

            try
            {
                var createBucketResponse = await bucketsApi.CreateBucketAsyncWithHttpInfo(
                    new PostBucketsPayload(bucketKey, null, PostBucketsPayload.PolicyKeyEnum.Persistent));
            }
            catch (Exception e)
            {

            }

            using (StreamReader reader = new StreamReader("VerticalPlate.ipt"))
            {
                var createInputObjectResponse = await objectsApi.UploadObjectAsyncWithHttpInfo(
                    bucketKey,
                    "IptInputFile",
                    (int)reader.BaseStream.Length,
                    reader.BaseStream,
                    "application/octet-stream",
                    null
                );

                var createOutputObjectResponse =
                    await objectsApi.UploadObjectAsyncWithHttpInfo(bucketKey, "SvfOutputFile", 0, new MemoryStream(), null,
                        null);
                var outputObject = createOutputObjectResponse.Data;
            }

            var createInputSignedResourceResult = await objectsApi.CreateSignedResourceAsyncWithHttpInfo(bucketKey, "IptInputFile", new PostBucketsSigned(100), "read");
            var createOutputSignedResourceResult = await objectsApi.CreateSignedResourceAsyncWithHttpInfo(bucketKey, "SvfOutputFile", new PostBucketsSigned(100), "readwrite");
            var signedInputResource = createInputSignedResourceResult.Data;
            var signedOutputResource = createOutputSignedResourceResult.Data;
            InputFileUrl = signedInputResource.signedUrl;
            OutputFileUrl = signedOutputResource.signedUrl;
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
            if (response.HttpResponse.StatusCode == HttpStatusCode.NotFound) // create activity
            {
                Console.WriteLine($"Creating activity '{Constants.Activity.Id}'");
                await Client.CreateActivityAsync(activity, Constants.Activity.Label);
                Console.WriteLine("Done");
            }
            else // add new activity version
            {
                Console.WriteLine("Found existing activity. Updating...");
                int version = await Client.UpdateActivityAsync(activity, Constants.Activity.Label);
                Console.WriteLine($"Created version #{version} for '{Constants.Activity.Id}' activity.");
            }
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
