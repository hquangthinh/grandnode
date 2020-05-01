using Grand.Core.Data;
using Grand.Core.Domain.Configuration;
using Grand.Services.Authentication.External;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Grand.Plugin.ExternalAuth.AppOAuth
{
    /// <summary>
    /// Registration of app oauth authentication service (plugin)
    /// </summary>
    public class AppOAuthAuthenticationRegistrar : IExternalAuthenticationRegistrar
    {
        public int Order => 503;

        public void Configure(AuthenticationBuilder builder, IConfiguration configuration)
        {
            builder.AddOAuth("AppOAuth", options => 
            {
                options.AuthorizationEndpoint = "";
                options.ClientId = "";
                options.ClientSecret = "";

            });
        }
    }
}
