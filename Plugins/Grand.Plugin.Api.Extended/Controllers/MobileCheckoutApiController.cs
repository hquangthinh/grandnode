using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Payments;
using Grand.Core.Domain.Shipping;
using Grand.Core.Http;
using Grand.Core.Plugins;
using Grand.Core;
using Grand.Framework.Controllers;
using Grand.Services.Catalog;
using Grand.Services.Common;
using Grand.Services.Customers;
using Grand.Services.Localization;
using Grand.Services.Logging;
using Grand.Services.Orders;
using Grand.Services.Payments;
using Grand.Services.Shipping;
using Grand.Web.Areas.Api.Controllers;
using Grand.Web.Extensions;
using Grand.Web.Interfaces;
using Grand.Web.Models.Checkout;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Grand.Plugin.Api.Extended.DTOs;
using Grand.Core.Domain.Common;
using Newtonsoft.Json;
using Grand.Web.Models.ShoppingCart;

namespace Grand.Plugin.Api.Extended.Controllers
{
    public class SaveAddressCommand
    {
        public Address BillingAddress { get; set; }

        public Address ShippingAddress { get; set; }

        public bool ShipToSameAddress { get; set; }
    }

    public class SaveAddressResponse
    {
        public Address BillingAddress { get; set; }

        public Address ShippingAddress { get; set; }
    }

    public class SaveShippingMethodCommand
    {
        public string ShippingOption { get; set; }
    }

    public class SaveShippingMethodResponse
    {
        public ShippingOption ShippingOption { get; set; }
    }

    public class SavePaymentMethodCommand
    {
        public string PaymentMethod { get; set; }

        public bool DisplayRewardPoints { get; set; }

        public int RewardPointsBalance { get; set; }

        public string RewardPointsAmount { get; set; }

        public bool RewardPointsEnoughToPayForOrder { get; set; }

        public bool UseRewardPoints { get; set; }
    }

    public class SavePaymentMethodResponse
    {
        public string PaymentMethod { get; set; }

        public CheckoutConfirmModel ConfirmOrderModel { get; set; }
    }

    public class PrepareForOrderConfirmationCommand
    {
        public Address BillingAddress { get; set; }

        public Address ShippingAddress { get; set; }

        public bool ShipToSameAddress { get; set; }

        public bool PickUpInStore { get; set; }

        public string PickupPointId { get; set; }

        public string ShippingOption { get; set; }

        public string PaymentMethod { get; set; }
    }

    public class PrepareForOrderConfirmationResponse
    {
        public SaveAddressResponse SaveAddressResponse { get; set; }

        public SaveShippingMethodResponse SaveShippingMethodResponse { get; set; }

        public string PaymentMethod { get; set; }

        public CheckoutConfirmModel ConfirmOrderModel { get; set; }

        public ShoppingCartModel CartModel { get; set; }

        public OrderTotalsModel OrderTotalsModel { get; set; }
    }

    public class ConfirmOrderResponse
    {
        public string RedirectToRoute { get; set; }
        public string OrderId { get; set; }
        public List<string> ErrorsOrWarnings { get; set; }
    }

    [Route("api/mobile-checkout")]
    public class MobileCheckoutApiController : BaseApiController
    {
        #region Fields

        private readonly ICheckoutViewModelService _checkoutViewModelService;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly ILocalizationService _localizationService;
        private readonly ICustomerService _customerService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IShippingService _shippingService;
        private readonly IPaymentService _paymentService;
        private readonly IPluginFinder _pluginFinder;
        private readonly ILogger _logger;
        private readonly IOrderService _orderService;
        private readonly IWebHelper _webHelper;
        private readonly IShoppingCartViewModelService _shoppingCartViewModelService;
        private readonly IAddressViewModelService _addressViewModelService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly OrderSettings _orderSettings;
        private readonly RewardPointsSettings _rewardPointsSettings;
        private readonly PaymentSettings _paymentSettings;
        private readonly ShippingSettings _shippingSettings;

        #endregion

        #region Constructors

        public MobileCheckoutApiController(
            ICheckoutViewModelService checkoutViewModelService,
            IWorkContext workContext,
            IStoreContext storeContext,
            ILocalizationService localizationService,
            ICustomerService customerService,
            IShoppingCartService shoppingCartService,
            IGenericAttributeService genericAttributeService,
            IShippingService shippingService,
            IPaymentService paymentService,
            IPluginFinder pluginFinder,
            ILogger logger,
            IOrderService orderService,
            IWebHelper webHelper,
            IAddressViewModelService addressViewModelService,
            IShoppingCartViewModelService shoppingCartViewModelService,
            IOrderProcessingService orderProcessingService,
            OrderSettings orderSettings,
            RewardPointsSettings rewardPointsSettings,
            PaymentSettings paymentSettings,
            ShippingSettings shippingSettings)
        {
            _checkoutViewModelService = checkoutViewModelService;
            _workContext = workContext;
            _storeContext = storeContext;
            _localizationService = localizationService;
            _customerService = customerService;
            _shoppingCartService = shoppingCartService;
            _genericAttributeService = genericAttributeService;
            _shippingService = shippingService;
            _paymentService = paymentService;
            _pluginFinder = pluginFinder;
            _logger = logger;
            _orderService = orderService;
            _webHelper = webHelper;
            _addressViewModelService = addressViewModelService;
            _shoppingCartViewModelService = shoppingCartViewModelService;
            _orderProcessingService = orderProcessingService;
            _orderSettings = orderSettings;
            _rewardPointsSettings = rewardPointsSettings;
            _paymentSettings = paymentSettings;
            _shippingSettings = shippingSettings;
        }

        #endregion

        #region Methods (multistep checkout)

        private async Task<ApiResponse<SaveAddressResponse>> SaveAddressInternalAsync(SaveAddressCommand command)
        {
            try
            {
                var address = _workContext.CurrentCustomer.Addresses.FirstOrDefault(a => a.Id == command.BillingAddress.Id);

                if (address != null)
                {
                    _workContext.CurrentCustomer.BillingAddress = address;
                    address.CustomerId = _workContext.CurrentCustomer.Id;
                    await _customerService.UpdateBillingAddress(address);
                }
                else
                {
                    //try to find an address with the same values (don't duplicate records)
                    address = _workContext.CurrentCustomer.Addresses.ToList().FindAddress(
                        command.BillingAddress.FirstName, command.BillingAddress.LastName, command.BillingAddress.PhoneNumber,
                        command.BillingAddress.Email, command.BillingAddress.FaxNumber, command.BillingAddress.Company,
                        command.BillingAddress.Address1, command.BillingAddress.Address2, command.BillingAddress.City,
                        command.BillingAddress.StateProvinceId, command.BillingAddress.ZipPostalCode,
                        command.BillingAddress.CountryId, command.BillingAddress.CustomAttributes);
                    if (address == null)
                    {
                        //address is not found. let's create a new one
                        address = command.BillingAddress;
                        address.CreatedOnUtc = DateTime.UtcNow;
                        _workContext.CurrentCustomer.Addresses.Add(address);
                        address.CustomerId = _workContext.CurrentCustomer.Id;
                        await _customerService.InsertAddress(address);

                        _workContext.CurrentCustomer.BillingAddress = address;
                        await _customerService.UpdateBillingAddress(address);
                    }
                    else
                    {
                        _workContext.CurrentCustomer.BillingAddress = address;
                        address.CustomerId = _workContext.CurrentCustomer.Id;
                        await _customerService.UpdateBillingAddress(address);
                    }
                }

                // shipping address
                var shipAaddress = _workContext.CurrentCustomer.Addresses.FirstOrDefault(a => a.Id == command.ShippingAddress.Id);

                if (shipAaddress != null)
                {
                    _workContext.CurrentCustomer.ShippingAddress = shipAaddress;
                    shipAaddress.CustomerId = _workContext.CurrentCustomer.Id;
                    await _customerService.UpdateShippingAddress(shipAaddress);
                }
                else
                {
                    //try to find an address with the same values (don't duplicate records)
                    shipAaddress = _workContext.CurrentCustomer.Addresses.ToList().FindAddress(
                        command.ShippingAddress.FirstName,
                        command.ShippingAddress.LastName,
                        command.ShippingAddress.PhoneNumber,
                        command.ShippingAddress.Email,
                        command.ShippingAddress.FaxNumber,
                        command.ShippingAddress.Company,
                        command.ShippingAddress.Address1,
                        command.ShippingAddress.Address2,
                        command.ShippingAddress.City,
                        command.ShippingAddress.StateProvinceId,
                        command.ShippingAddress.ZipPostalCode,
                        command.ShippingAddress.CountryId,
                        command.ShippingAddress.CustomAttributes
                    );
                    if (shipAaddress == null)
                    {
                        //address is not found. let's create a new one
                        shipAaddress = command.ShippingAddress;
                        shipAaddress.CreatedOnUtc = DateTime.UtcNow;
                        _workContext.CurrentCustomer.Addresses.Add(shipAaddress);
                        shipAaddress.CustomerId = _workContext.CurrentCustomer.Id;
                        await _customerService.InsertAddress(shipAaddress);

                        _workContext.CurrentCustomer.ShippingAddress = shipAaddress;
                        await _customerService.UpdateShippingAddress(shipAaddress);
                    }
                    else
                    {
                        _workContext.CurrentCustomer.ShippingAddress = shipAaddress;
                        shipAaddress.CustomerId = _workContext.CurrentCustomer.Id;
                        await _customerService.UpdateShippingAddress(shipAaddress);
                    }
                }

                return ApiResponse<SaveAddressResponse>.SuccessResult(new SaveAddressResponse {
                    BillingAddress = _workContext.CurrentCustomer.BillingAddress ?? address,
                    ShippingAddress = _workContext.CurrentCustomer.ShippingAddress ?? address
                });
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return ApiResponse<SaveAddressResponse>.FailResult(exc.Message);
            }
        }

        [HttpPost]
        [Route("save-address")]
        public virtual async Task<IActionResult> SaveAddress(SaveAddressCommand command)
        {
            return Ok(await SaveAddressInternalAsync(command));
        }

        [HttpGet]
        [Route("shipping-method")]
        public virtual async Task<IActionResult> GetCartShippingMethod()
        {
            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            var shippingMethodModel = await _checkoutViewModelService.PrepareShippingMethod(cart, _workContext.CurrentCustomer.ShippingAddress);

            return Ok(ApiResponse<CheckoutShippingMethodModel>.SuccessResult(shippingMethodModel));
        }

        [HttpGet]
        [Route("payment-method")]
        public virtual async Task<IActionResult> GetCartPaymentMethod()
        {
            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            //filter by country
            var filterByCountryId = "";
            if (_addressViewModelService.AddressSettings().CountryEnabled &&
                _workContext.CurrentCustomer.BillingAddress != null &&
                !string.IsNullOrEmpty(_workContext.CurrentCustomer.BillingAddress.CountryId))
            {
                filterByCountryId = _workContext.CurrentCustomer.BillingAddress.CountryId;
            }

            var paymentMethodModel = await _checkoutViewModelService.PreparePaymentMethod(cart, filterByCountryId);

            return Ok(ApiResponse<CheckoutPaymentMethodModel>.SuccessResult(paymentMethodModel));
        }

        private async Task<ApiResponse<SaveShippingMethodResponse>> SaveShippingMethodInternalAsync(
            SaveShippingMethodCommand command, IList<ShoppingCartItem> cartItems)
        {
            if (command == null)
            {
                return ApiResponse<SaveShippingMethodResponse>.FailResult("Checkout.SaveShippingMethod.InvalidParameter");
            }

            if (string.IsNullOrEmpty(command.ShippingOption))
            {
                return ApiResponse<SaveShippingMethodResponse>.FailResult("Checkout.SaveShippingMethod.ShippingOption.Required");
            }

            // parse selected shipping method 
            string shippingoption = command.ShippingOption;

            var splittedOption = shippingoption.Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
            if (splittedOption.Length != 2)
            {
                return ApiResponse<SaveShippingMethodResponse>.FailResult("Checkout.SaveShippingMethod.ShippingOption.InvalidFormat");
            }
            string selectedName = splittedOption[0];
            string shippingRateComputationMethodSystemName = splittedOption[1];

            try
            {
                //validation
                var customer = _workContext.CurrentCustomer;
                var store = _storeContext.CurrentStore;

                var cart = cartItems ?? _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

                //clear shipping option XML/Description
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.ShippingOptionAttributeXml, "", store.Id);
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.ShippingOptionAttributeDescription, "", store.Id);

                //find it
                //performance optimization. try cache first
                var shippingOptions = await customer.GetAttribute<List<ShippingOption>>(_genericAttributeService, SystemCustomerAttributeNames.OfferedShippingOptions, store.Id);
                if (shippingOptions == null || shippingOptions.Count == 0)
                {
                    //not found? let's load them using shipping service
                    shippingOptions = (await _shippingService
                        .GetShippingOptions(customer, cart, customer.ShippingAddress, shippingRateComputationMethodSystemName, store))
                        .ShippingOptions
                        .ToList();
                }
                else
                {
                    //loaded cached results. let's filter result by a chosen shipping rate computation method
                    shippingOptions = shippingOptions.Where(so => so.ShippingRateComputationMethodSystemName.Equals(shippingRateComputationMethodSystemName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var shippingOption = shippingOptions
                    .Find(so => !string.IsNullOrEmpty(so.Name) && so.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
                if (shippingOption == null)
                {
                    return ApiResponse<SaveShippingMethodResponse>.FailResult("Checkout.SaveShippingMethod.SelectedShippingMethodCanNotBeLoaded");
                }

                //save
                await _genericAttributeService.SaveAttribute(customer, SystemCustomerAttributeNames.SelectedShippingOption, shippingOption, store.Id);

                return ApiResponse<SaveShippingMethodResponse>.SuccessResult(new SaveShippingMethodResponse { ShippingOption = shippingOption });
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return ApiResponse<SaveShippingMethodResponse>.FailResult(exc.Message);
            }
        }
        
        [HttpPost]
        [Route("save-shipping-method")]
        public virtual async Task<IActionResult> SaveShippingMethod([FromBody] SaveShippingMethodCommand command)
        {
            return Ok(await SaveShippingMethodInternalAsync(command, cartItems: null));
        }

        private async Task<ApiResponse<SavePaymentMethodResponse>> SavePaymentMethodInternalAsync(SavePaymentMethodCommand command, IList<ShoppingCartItem> cartItems)
        {
            if(command == null)
            {
                return ApiResponse<SavePaymentMethodResponse>.FailResult("Checkout.SavePaymentMethod.InvalidParameter");
            }

            if (string.IsNullOrEmpty(command.PaymentMethod))
                return ApiResponse<SavePaymentMethodResponse>.FailResult("Checkout.SavePaymentMethod.PaymentMethod.Required");

            try
            {
                //validation
                var cart = cartItems ?? _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

                //reward points
                if (_rewardPointsSettings.Enabled)
                {
                    await _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                        SystemCustomerAttributeNames.UseRewardPointsDuringCheckout, command.UseRewardPoints,
                        _storeContext.CurrentStore.Id);
                }

                var paymentMethodInst = _paymentService.LoadPaymentMethodBySystemName(command.PaymentMethod);
                if (paymentMethodInst == null ||
                    !paymentMethodInst.IsPaymentMethodActive(_paymentSettings) ||
                    !_pluginFinder.AuthenticateStore(paymentMethodInst.PluginDescriptor, _storeContext.CurrentStore.Id))
                {
                    return ApiResponse<SavePaymentMethodResponse>.FailResult("Checkout.SavePaymentMethod.InvalidPaymentMethod", new SavePaymentMethodResponse { 
                        PaymentMethod = command.PaymentMethod
                    });
                }

                //save
                await _genericAttributeService.SaveAttribute(_workContext.CurrentCustomer,
                    SystemCustomerAttributeNames.SelectedPaymentMethod, command.PaymentMethod, _storeContext.CurrentStore.Id);

                //skip payment info page - COD as default payment method
                var paymentInfo = new ProcessPaymentRequest();
                //session save
                this.HttpContext.Session.Set("OrderPaymentInfo", paymentInfo);

                var confirmOrderModel = await _checkoutViewModelService.PrepareConfirmOrder(cart);

                return ApiResponse<SavePaymentMethodResponse>.SuccessResult(new SavePaymentMethodResponse {
                    PaymentMethod = command.PaymentMethod,
                    ConfirmOrderModel = confirmOrderModel
                });
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc, _workContext.CurrentCustomer);
                return ApiResponse<SavePaymentMethodResponse>.FailResult(exc.Message, new SavePaymentMethodResponse {
                    PaymentMethod = command.PaymentMethod
                });
            }
        }

        [HttpPost]
        [Route("save-payment-method")]
        public virtual async Task<IActionResult> SavePaymentMethod([FromBody] SavePaymentMethodCommand command)
        {
            return Ok(await SavePaymentMethodInternalAsync(command, cartItems: null));
        }

        /// <summary>
        /// Prepare shopping cart for order confirmation
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("prepare-for-order-confirmation")]
        public virtual async Task<IActionResult> PrepareForOrderConfirmation([FromBody] PrepareForOrderConfirmationCommand command)
        {
            if(command == null)
            {
                return Ok(ApiResponse<PrepareForOrderConfirmationResponse>.FailResult("Checkout.PrepareForOrderConfirmation.InvalidParameters"));
            }

            var saveAddressRes = await SaveAddressInternalAsync(new SaveAddressCommand {
                BillingAddress = command.BillingAddress,
                ShippingAddress = command.ShippingAddress,
                ShipToSameAddress = command.ShipToSameAddress
            });

            if(saveAddressRes.HasError)
            {
                return Ok(ApiResponse<PrepareForOrderConfirmationResponse>.FailResult(saveAddressRes.Message));
            }

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            var saveShippingMethodRes = await SaveShippingMethodInternalAsync(new SaveShippingMethodCommand {
                ShippingOption = command.ShippingOption
            }, cartItems: cart);

            if (saveShippingMethodRes.HasError)
            {
                return Ok(ApiResponse<PrepareForOrderConfirmationResponse>.FailResult(saveShippingMethodRes.Message));
            }

            var savePaymentMethodRes = await SavePaymentMethodInternalAsync(new SavePaymentMethodCommand { 
                PaymentMethod = command.PaymentMethod
            }, cartItems: cart);

            if (savePaymentMethodRes.HasError)
            {
                return Ok(ApiResponse<PrepareForOrderConfirmationResponse>.FailResult(savePaymentMethodRes.Message));
            }

            var cartModel = new ShoppingCartModel();

            await _shoppingCartViewModelService.PrepareShoppingCart(cartModel, cart);

            var orderTotalsModel = await _shoppingCartViewModelService.PrepareOrderTotals(cart, isEditable: false);

            return Ok(ApiResponse<PrepareForOrderConfirmationResponse>.SuccessResult(new PrepareForOrderConfirmationResponse {
                SaveAddressResponse = saveAddressRes.Data,
                SaveShippingMethodResponse = saveShippingMethodRes.Data,
                PaymentMethod = savePaymentMethodRes.Data.PaymentMethod,
                ConfirmOrderModel = savePaymentMethodRes.Data.ConfirmOrderModel,
                CartModel = cartModel,
                OrderTotalsModel = orderTotalsModel
            }));
        }

        /// <summary>
        /// Proceed payment and confirm the order
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("confirm-order")]
        public virtual async Task<IActionResult> ConfirmOrder()
        {
            //validation
            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            if (!cart.Any())
            {
                return Ok(ApiResponse<ConfirmOrderResponse>.FailResult("Checkout.ConfirmOrder.CartIsEmpty"));
            }

            if (_workContext.CurrentCustomer.IsGuest() && !_orderSettings.AnonymousCheckoutAllowed)
            {
                return Ok(ApiResponse<ConfirmOrderResponse>.FailResult("Checkout.ConfirmOrder.AnonymousCheckoutIsNotAllowed"));
            }

            var errorsOrWarnings = new List<string>();

            try
            {
                var processPaymentRequest = this.HttpContext.Session.Get<ProcessPaymentRequest>("OrderPaymentInfo");
                if (processPaymentRequest == null)
                {
                    //Check whether payment workflow is required
                    //if (await _checkoutViewModelService.IsPaymentWorkflowRequired(cart))
                    //{
                    //    return Ok(ApiResponse<string>.FailResult("Checkout.ConfirmOrder.IsPaymentWorkflowRequired"));
                    //}

                    processPaymentRequest = new ProcessPaymentRequest();
                }

                //prevent 2 orders being placed within an X seconds time frame
                if (!await _checkoutViewModelService.IsMinimumOrderPlacementIntervalValid(_workContext.CurrentCustomer))
                    throw new Exception(_localizationService.GetResource("Checkout.MinOrderPlacementInterval"));

                //place order
                processPaymentRequest.StoreId = _storeContext.CurrentStore.Id;
                processPaymentRequest.CustomerId = _workContext.CurrentCustomer.Id;
                processPaymentRequest.PaymentMethodSystemName = await _workContext.CurrentCustomer.GetAttribute<string>(
                    _genericAttributeService, SystemCustomerAttributeNames.SelectedPaymentMethod,
                    _storeContext.CurrentStore.Id);
                var placeOrderResult = await _orderProcessingService.PlaceOrder(processPaymentRequest);
                if (placeOrderResult.Success)
                {
                    this.HttpContext.Session.Set<ProcessPaymentRequest>("OrderPaymentInfo", null);
                    var postProcessPaymentRequest = new PostProcessPaymentRequest {
                        Order = placeOrderResult.PlacedOrder
                    };
                    await _paymentService.PostProcessPayment(postProcessPaymentRequest);

                    if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
                    {
                        // redirection or POST has been done in PostProcessPayment
                        return Ok(ApiResponse<ConfirmOrderResponse>.SuccessResult(new ConfirmOrderResponse {
                            RedirectToRoute = "Redirected_InPostProcessPayment",
                            OrderId = placeOrderResult.PlacedOrder.Id
                        }));
                    }

                    return Ok(ApiResponse<ConfirmOrderResponse>.SuccessResult(new ConfirmOrderResponse { 
                        RedirectToRoute = "CheckoutCompleted",
                        OrderId = placeOrderResult.PlacedOrder.Id
                    }));
                }

                foreach (var error in placeOrderResult.Errors)
                    errorsOrWarnings.Add(error);
            }
            catch (Exception exc)
            {
                _logger.Warning(exc.Message, exc);
                errorsOrWarnings.Add(exc.Message);
            }

            //If we got this far, something failed, redisplay form
            var message = string.Join("\n", errorsOrWarnings);
            return Ok(ApiResponse<ConfirmOrderResponse>.FailResult(message));
        }

        #endregion
    }
}
