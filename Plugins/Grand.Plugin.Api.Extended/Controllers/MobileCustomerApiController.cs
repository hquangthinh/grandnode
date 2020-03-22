using System.Threading.Tasks;
using Grand.Core;
using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Localization;
using Grand.Core.Domain.Tax;
using Grand.Framework.Security.Captcha;
using Grand.Services.Authentication;
using Grand.Services.Common;
using Grand.Services.Customers;
using Grand.Services.Directory;
using Grand.Services.Helpers;
using Grand.Services.Localization;
using Grand.Services.Logging;
using Grand.Services.Messages;
using Grand.Services.Tax;
using Grand.Web.Interfaces;
using Grand.Web.Models.Customer;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Plugin.Api.Extended.Controllers
{
    [Route("api/mobile-customer")]
    public class MobileCustomerApiController : BaseAuthorizedApiController
    {
        #region Fields

        private readonly ICustomerViewModelService _customerViewModelService;
        private readonly IGrandAuthenticationService _authenticationService;
        private readonly ILocalizationService _localizationService;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly ICustomerService _customerService;
        private readonly ICustomerAttributeParser _customerAttributeParser;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ICustomerRegistrationService _customerRegistrationService;
        private readonly ITaxService _taxService;
        private readonly ICountryService _countryService;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly IAddressViewModelService _addressViewModelService;
        private readonly IMediator _mediator;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly CustomerSettings _customerSettings;
        private readonly DateTimeSettings _dateTimeSettings;
        private readonly TaxSettings _taxSettings;
        private readonly LocalizationSettings _localizationSettings;
        private readonly CaptchaSettings _captchaSettings;

        #endregion

        #region Ctor

        public MobileCustomerApiController(
            ICustomerViewModelService customerViewModelService,
            IGrandAuthenticationService authenticationService,
            ILocalizationService localizationService,
            IWorkContext workContext,
            IStoreContext storeContext,
            ICustomerService customerService,
            ICustomerAttributeParser customerAttributeParser,
            IGenericAttributeService genericAttributeService,
            ICustomerRegistrationService customerRegistrationService,
            ITaxService taxService,
            ICountryService countryService,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            ICustomerActivityService customerActivityService,
            IAddressViewModelService addressViewModelService,
            IMediator mediator,
            IWorkflowMessageService workflowMessageService,
            CaptchaSettings captchaSettings,
            CustomerSettings customerSettings,
            DateTimeSettings dateTimeSettings,
            LocalizationSettings localizationSettings,
            TaxSettings taxSettings
            )
        {
            _customerViewModelService = customerViewModelService;
            _authenticationService = authenticationService;
            _dateTimeSettings = dateTimeSettings;
            _taxSettings = taxSettings;
            _localizationService = localizationService;
            _workContext = workContext;
            _storeContext = storeContext;
            _customerService = customerService;
            _customerAttributeParser = customerAttributeParser;
            _genericAttributeService = genericAttributeService;
            _customerRegistrationService = customerRegistrationService;
            _taxService = taxService;
            _customerSettings = customerSettings;
            _countryService = countryService;
            _newsLetterSubscriptionService = newsLetterSubscriptionService;
            _customerActivityService = customerActivityService;
            _addressViewModelService = addressViewModelService;
            _workflowMessageService = workflowMessageService;
            _localizationSettings = localizationSettings;
            _captchaSettings = captchaSettings;
            _mediator = mediator;
        }

        #endregion
        
        [Route("info")]
        public virtual async Task<IActionResult> Info()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;

            var model = new CustomerInfoModel();
            
            model = await _customerViewModelService.PrepareInfoModel(model, customer, false);

            return Ok(model);
        }
    }
}