using Grand.Core;
using Grand.Core.Plugins;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using System;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended
{
    public partial class ApiPlugin : BasePlugin
    {
        #region Fields

        private readonly ISettingService _settingService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebHelper _webHelper;

        #endregion

        public ApiPlugin(ISettingService settingService,
            IServiceProvider serviceProvider,
            IWebHelper webHelper)
        {
            _settingService = settingService;
            _serviceProvider = serviceProvider;
            _webHelper = webHelper;
        }

        public override async Task Install()
        {
            //settings
            await _settingService.SaveSetting(new ApiExtendedSettings());

            //locales
            await this.AddOrUpdatePluginLocaleResource(_serviceProvider, "Plugins.Api.Extended.FirebaseAppCredentialConfigurationFile", "Path to Firebase app credential configuration file");

            await base.Install();
        }

        public override async Task Uninstall()
        {
            //settings
            await _settingService.DeleteSetting<ApiExtendedSettings>();

            //locales
            await this.DeletePluginLocaleResource(_serviceProvider, "Plugins.Api.Extended.FirebaseAppCredentialConfigurationFile");

            await base.Uninstall();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/ApiExtendedAdmin/Configure";
        }
    }
}
