using Grand.Core;
using Grand.Core.Plugins;
using Grand.Services.Authentication.External;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using System;
using System.Threading.Tasks;

namespace Grand.Plugin.ExternalAuth.AppOAuth
{
    public class AppOAuthAuthenticationMethod : BasePlugin, IExternalAuthenticationMethod
    {
        #region Fields

        private readonly ISettingService _settingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public AppOAuthAuthenticationMethod(ISettingService settingService,
            IServiceProvider serviceProvider,
            IWebHelper webHelper)
        {
            _settingService = settingService;
            _serviceProvider = serviceProvider;
            _webHelper = webHelper;
        }

        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "AppOAuthAuthentication";
        }

        #endregion

        #region Methods

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override async Task Install()
        {
            await base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override async Task Uninstall()
        {
            await base.Uninstall();
        }

        #endregion
    }
}
