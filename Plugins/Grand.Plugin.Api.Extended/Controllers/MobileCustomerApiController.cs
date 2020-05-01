using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grand.Core;
using Grand.Core.Domain.Common;
using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Localization;
using Grand.Core.Domain.Tax;
using Grand.Framework.Security.Captcha;
using Grand.Plugin.Api.Extended.DTOs;
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
using Microsoft.AspNetCore.Authorization;
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
        private readonly IAddressService _addressService;
        private readonly ICustomerActionEventService _customerActionEventService;
        private readonly IWebHelper _webHelper;
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
            IAddressService addressService,
            ICustomerActionEventService customerActionEventService,
            IWebHelper webHelper,
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
            _addressService = addressService;
            _customerActionEventService = customerActionEventService;
            _webHelper = webHelper;
        }

        #endregion

        /// <summary>
        /// Get current customer main information
        /// </summary>
        /// <returns></returns>
        [Route("info")]
        public virtual async Task<IActionResult> GetCurrentCustomerInfo()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;

            var model = new CustomerInfoModel();
            
            model = await _customerViewModelService.PrepareInfoModel(model, customer, false);

            return Ok(model);
        }

        [Route("addresses")]
        public virtual async Task<IActionResult> GetCurrentCustomerAddresses()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;

            var addresses = await _customerViewModelService.PrepareAddressList(customer);

            return Ok(addresses);
        }

        [Route("delete-address/{addressId}")]
        public virtual async Task<IActionResult> DeleteAddress(string addressId)
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Challenge();

            var customer = _workContext.CurrentCustomer;

            //find address (ensure that it belongs to the current customer)
            var address = customer.Addresses.FirstOrDefault(a => a.Id == addressId);
            if (address != null)
            {
                customer.RemoveAddress(address);
                address.CustomerId = customer.Id;
                await _customerService.DeleteAddress(address);
            }

            return await GetCurrentCustomerAddresses();
        }


        [Route("register")]
        [HttpPost]
        [AllowAnonymous]
        public virtual async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            var result = new UserRegistrationResult();

            if (_customerSettings.UserRegistrationType == UserRegistrationType.Disabled)
            {
                return Ok(ApiResponse<UserRegistrationResult>.FailResult("Customer.RegisterDisabled"));
            }

            if (_workContext.CurrentCustomer.IsRegistered())
            {
                //Already registered customer. 
                await _authenticationService.SignOut();

                //Save a new record
                _workContext.CurrentCustomer = await _customerService.InsertGuestCustomer(_storeContext.CurrentStore);
            }
            var customer = _workContext.CurrentCustomer;

            // validate request
            if (model == null || !ModelState.IsValid)
            {
                var validateRes = ApiResponse<UserRegistrationResult>.FailResult("Customer.RegisterHasMultipleErrors");
                foreach (var err in ModelState)
                {
                    validateRes.AggregateErrors.AddRange(err.Value.Errors.Select(e => e.ErrorMessage));
                }
                return Ok(validateRes);
            }

            if (_customerSettings.UsernamesEnabled && model.Email != null)
            {
                model.Username = model.Email.Trim();
            }

            var isApproved = _customerSettings.UserRegistrationType == UserRegistrationType.Standard;

            var registrationRequest = new CustomerRegistrationRequest(customer, model.Email,
                _customerSettings.UsernamesEnabled ? model.Username : model.Email, model.Password,
                _customerSettings.DefaultPasswordFormat, _storeContext.CurrentStore.Id, isApproved);

            var registrationResult = await _customerRegistrationService.RegisterCustomer(registrationRequest);

            if(!registrationResult.Success)
            {
                var registrationRes = ApiResponse<UserRegistrationResult>.FailResult("Customer.RegisterHasMultipleErrors");
                registrationRes.AggregateErrors.AddRange(registrationResult.Errors);
                return Ok(registrationRes);
            }

            //properties
            if (_dateTimeSettings.AllowCustomersToSetTimeZone)
            {
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.TimeZoneId, model.TimeZoneId);
            }

            //VAT number
            if (_taxSettings.EuVatEnabled)
            {
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.VatNumber, model.VatNumber);

                var vat = await _taxService.GetVatNumberStatus(model.VatNumber);

                await _genericAttributeService.SaveAttribute(customer,
                    SystemCustomerAttributeNames.VatNumberStatusId,
                    (int)vat.status);

                //send VAT number admin notification
                if (!string.IsNullOrEmpty(model.VatNumber) && _taxSettings.EuVatEmailAdminWhenNewVatSubmitted)
                    await _workflowMessageService.SendNewVatSubmittedStoreOwnerNotification(customer, model.VatNumber, vat.address, _localizationSettings.DefaultAdminLanguageId);
            }

            //form fields
            if (_customerSettings.GenderEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.Gender, model.Gender);
            await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.FirstName, model.FirstName);
            await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.LastName, model.LastName);
            if (_customerSettings.DateOfBirthEnabled)
            {
                DateTime? dateOfBirth = model.ParseDateOfBirth();
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.DateOfBirth, dateOfBirth);
            }
            if (_customerSettings.CompanyEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.Company, model.Company);
            if (_customerSettings.StreetAddressEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.StreetAddress, model.StreetAddress);
            if (_customerSettings.StreetAddress2Enabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.StreetAddress2, model.StreetAddress2);
            if (_customerSettings.ZipPostalCodeEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.ZipPostalCode, model.ZipPostalCode);
            if (_customerSettings.CityEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.City, model.City);
            if (_customerSettings.CountryEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.CountryId, model.CountryId);
            if (_customerSettings.CountryEnabled && _customerSettings.StateProvinceEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.StateProvinceId, model.StateProvinceId);
            if (_customerSettings.PhoneEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.Phone, model.Phone);
            if (_customerSettings.FaxEnabled)
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.Fax, model.Fax);

            //save customer attributes
            //await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.CustomCustomerAttributes, customerAttributesXml);

            //login customer now
            if (isApproved)
            {
                await _authenticationService.SignIn(customer, true);
            }

            //insert default address (if possible)
            var defaultAddress = new Address {
                FirstName = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.FirstName),
                LastName = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.LastName),
                Email = customer.Email,
                Company = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.Company),
                VatNumber = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.VatNumber),
                CountryId = !String.IsNullOrEmpty(await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.CountryId)) ?
                    await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.CountryId) : "",
                StateProvinceId = !String.IsNullOrEmpty(await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.StateProvinceId)) ?
                    await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.StateProvinceId) : "",
                City = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.City),
                Address1 = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.StreetAddress),
                Address2 = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.StreetAddress2),
                ZipPostalCode = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.ZipPostalCode),
                PhoneNumber = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.Phone),
                FaxNumber = await customer.GetAttribute<string>(_genericAttributeService, SystemCustomerAttributeNames.Fax),
                CreatedOnUtc = customer.CreatedOnUtc,
            };

            if (await _addressService.IsAddressValid(defaultAddress))
            {
                //set default address
                defaultAddress.CustomerId = customer.Id;
                customer.Addresses.Add(defaultAddress);
                await _customerService.InsertAddress(defaultAddress);
                customer.BillingAddress = defaultAddress;
                await _customerService.UpdateBillingAddress(defaultAddress);
                customer.ShippingAddress = defaultAddress;
                await _customerService.UpdateShippingAddress(defaultAddress);
            }

            //notifications
            if (_customerSettings.NotifyNewCustomerRegistration)
                await _workflowMessageService.SendCustomerRegisteredNotificationMessage(customer, _localizationSettings.DefaultAdminLanguageId);

            //New customer has a free shipping for the first order
            if (_customerSettings.RegistrationFreeShipping)
                await _customerService.UpdateFreeShipping(customer.Id, true);

            await _customerActionEventService.Registration(customer);

            //raise event
            await _mediator.Publish(new CustomerRegisteredEvent(customer));

            switch (_customerSettings.UserRegistrationType)
            {
                case UserRegistrationType.EmailValidation:
                    {
                        //email validation message
                        await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.AccountActivationToken, Guid.NewGuid().ToString());
                        await _workflowMessageService.SendCustomerEmailValidationMessage(customer, _workContext.WorkingLanguage.Id);

                        result.RegistrationStatus = "EmailValidation";
                        break;
                    }
                case UserRegistrationType.AdminApproval:
                    {
                        result.RegistrationStatus = "AdminApproval";
                        break;
                    }
                case UserRegistrationType.Standard:
                default:
                    {
                        //send customer welcome message
                        await _workflowMessageService.SendCustomerWelcomeMessage(customer, _workContext.WorkingLanguage.Id);
                        result.RegistrationStatus = "Standard";
                        break;
                    }
            }

            result.CustomerInfo = await _customerViewModelService.PrepareInfoModel(new CustomerInfoModel(), customer, false);

            return Ok(ApiResponse<UserRegistrationResult>.SuccessResult(result));
        }
    }

    public class UserRegistrationResult
    {
        public string RegistrationStatus { get; set; }
        public CustomerInfoModel CustomerInfo { get; set; }
    }

}