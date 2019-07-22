using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Autodesk.Forge;
using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using Autodesk.Forge.DesignAutomation.Model;
using Autodesk.Forge.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RestSharp.Extensions;
using Viewer.Models;
using IWorkItemsApi = Autodesk.Forge.DesignAutomation.Http.IWorkItemsApi;
using WorkItem = Autodesk.Forge.DesignAutomation.Model.WorkItem;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Viewer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DaController : ControllerBase
    {
        private readonly DesignAutomationClient _daClient;
        private readonly IWorkItemsApi _workItemsApi;
        private readonly ForgeConfiguration _forgeConfig;
        private IHostingEnvironment _hostingEnvironment;
        private readonly IHttpClientFactory _httpClientFactory;

        public DaController(DesignAutomationClient daClient, IWorkItemsApi workItemsApi, IOptions<ForgeConfiguration> forgeConfig, IHostingEnvironment hostingEnvironment, IHttpClientFactory httpClientFactory)
        {
            _daClient = daClient;
            _workItemsApi = workItemsApi;
            _forgeConfig = forgeConfig.Value;
            _hostingEnvironment = hostingEnvironment;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("workitem")]
        public async Task<IActionResult> Post(IList<IFormFile> files)
        {
            var inputFile = files[0];

            var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "inputFile.ipt");
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await inputFile.CopyToAsync(fileStream);
            }

            Scope[] scope = new Scope[] { Scope.BucketCreate, Scope.BucketUpdate, Scope.DataRead, Scope.DataWrite };
            TwoLeggedApi twoLeggedApi = new TwoLeggedApi();
            
            Autodesk.Forge.Client.ApiResponse<dynamic> bearerResponse = await twoLeggedApi.AuthenticateAsyncWithHttpInfo(_forgeConfig.ClientId, _forgeConfig.ClientSecret, oAuthConstants.CLIENT_CREDENTIALS, scope);
            if (bearerResponse.StatusCode != 200)
            {
                throw new Exception("Request failed! (with HTTP response " + bearerResponse.StatusCode + ")");
            }

            var bearer = bearerResponse.Data;
            var bucketKey = _forgeConfig.ClientId.ToLower();
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
            
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                
                var length = (int)stream.Length;
                var createInputObjectResponse = await objectsApi.UploadObjectAsyncWithHttpInfo(
                    bucketKey,
                    "IptInputFile",
                    length,
                    stream,
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


            string nickname = await _daClient.GetNicknameAsync("me");

            var workItemRequest = new WorkItem()
            {
                ActivityId = $"{nickname}.ExportToSvf+alpha",
                Arguments = new Dictionary<string, IArgument>
                {
                    {
                        "InventorDoc",
                        new XrefTreeArgument
                        {
                            Url = signedInputResource.signedUrl
                        }
                    },
                    {
                        "OutputZip",
                        new XrefTreeArgument
                        {
                            Verb = Verb.Put,
                            Url = signedOutputResource.signedUrl
                        }
                    }
                }
            };

            var workItemResponse = await _workItemsApi.CreateWorkItemAsync(workItemRequest);

            return Ok(new WorkItemWithStatus(workItemRequest, workItemResponse.Content));
        }

        [HttpGet("workitem/{workitemId}")]
        public async Task<IActionResult> Get([FromRoute]string workitemId)
        {
            var workitemStatusResponse = await _workItemsApi.GetWorkitemStatusAsync(workitemId);
            return Ok(workitemStatusResponse.Content);
        }

        [HttpPost("downloads")]
        public async Task<IActionResult> PostDownloadRequest([FromBody] WorkItemWithStatus workitemWithStatus)
        {
            var resultsDirPath = Path.Combine(_hostingEnvironment.WebRootPath, "results");
            var resultsZipPath = Path.Combine(resultsDirPath, workitemWithStatus.Status.Id + ".zip");
            var resultsUnpackagedDirPath = Path.Combine(resultsDirPath, workitemWithStatus.Status.Id);

            Directory.CreateDirectory(resultsDirPath);
            

            var outputArgument = workitemWithStatus.WorkItem.Arguments["OutputZip"] as XrefTreeArgument;
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(outputArgument.Url);
            using (var fileStream = new FileStream(resultsZipPath, FileMode.Create))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            ZipFile.ExtractToDirectory(resultsZipPath, resultsUnpackagedDirPath);

            return Ok();
        }
    }
}
