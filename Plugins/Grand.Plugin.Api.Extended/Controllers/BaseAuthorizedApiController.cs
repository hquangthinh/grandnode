using Grand.Web.Areas.Api.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Grand.Plugin.Api.Extended.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BaseAuthorizedApiController : BaseApiController
    {
        
    }
}