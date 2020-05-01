using Grand.Framework.Mvc.ModelBinding;
using Grand.Framework.Mvc.Models;

namespace Grand.Plugin.Api.Extended.Models
{
    public class ConfigurationModel : BaseGrandModel
    {
        [GrandResourceDisplayName("Plugins.Api.Extended.FirebaseAppCredentialConfigurationFile")]
        public string FirebaseAppCredentialConfigurationFile { get; set; }
    }
}
