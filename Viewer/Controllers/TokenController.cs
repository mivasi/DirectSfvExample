using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Forge;
using Autodesk.Forge.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Viewer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly ForgeConfiguration _forgeConfig;
        private readonly TwoLeggedApi _twoLeggedApi;

        public TokenController(IOptions<ForgeConfiguration> forgeConfig, TwoLeggedApi twoLeggedApi)
        {
            this._forgeConfig = forgeConfig.Value;
            this._twoLeggedApi = twoLeggedApi;
        }

        // GET api/token
        [HttpGet]
        public async Task<ActionResult<dynamic>> Get()
        {
            Scope[] scope = { Scope.BucketCreate, Scope.BucketUpdate, Scope.DataRead, Scope.DataWrite };
            
            Autodesk.Forge.Client.ApiResponse<dynamic> bearerResponse = 
                await _twoLeggedApi.AuthenticateAsyncWithHttpInfo(_forgeConfig.ClientId, _forgeConfig.ClientSecret, oAuthConstants.CLIENT_CREDENTIALS, scope);
            if (bearerResponse.StatusCode != 200)
            {
                throw new Exception("Request failed! (with HTTP response " + bearerResponse.StatusCode + ")");
            }

            return bearerResponse.Data;
        }
    }
}
