using Grand.Framework.Controllers;
using Grand.Framework.Mvc.Filters;
using Grand.Plugin.Api.Extended.Models;
using Grand.Services.Configuration;
using Grand.Services.Localization;
using Grand.Services.Security;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended.Controllers
{
    public class ApiExtendedAdminController : BasePluginController
    {
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly ApiExtendedSettings _apiExtendedSettings;

        public ApiExtendedAdminController(
            IPermissionService permissionService,
            ILocalizationService localizationService,
            ISettingService settingService,
            ApiExtendedSettings apiExtendedSettings
        )
        {
            _permissionService = permissionService;
            _localizationService = localizationService;
            _settingService = settingService;
            _apiExtendedSettings = apiExtendedSettings;
        }

        [AuthorizeAdmin]
        [Area("Admin")]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.AccessAdminPanel))
                return AccessDeniedView();

            var model = new ConfigurationModel {
                FirebaseAppCredentialConfigurationFile = _apiExtendedSettings.FirebaseAppCredentialConfigurationFile
            };

            return View("~/Plugins/Api.Extended/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [AuthorizeAdmin]
        [Area("Admin")]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.AccessAdminPanel))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            _apiExtendedSettings.FirebaseAppCredentialConfigurationFile = model.FirebaseAppCredentialConfigurationFile;
            
            await _settingService.SaveSetting(_apiExtendedSettings);

            //now clear settings cache
            await _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return await Configure();
        }
    }
}
