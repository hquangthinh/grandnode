using Grand.Core.Domain.Catalog;
using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Media;
using Grand.Core.Domain.Orders;
using Grand.Core;
using Grand.Framework.Controllers;
using Grand.Framework.Mvc.Filters;
using Grand.Framework.Security.Captcha;
using Grand.Plugin.Api.Extended.DTOs;
using Grand.Services.Catalog;
using Grand.Services.Common;
using Grand.Services.Customers;
using Grand.Services.Discounts;
using Grand.Services.Localization;
using Grand.Services.Logging;
using Grand.Services.Media;
using Grand.Services.Messages;
using Grand.Services.Orders;
using Grand.Services.Security;
using Grand.Services.Seo;
using Grand.Web.Areas.Api.Controllers;
using Grand.Web.Interfaces;
using Grand.Web.Models.ShoppingCart;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using Grand.Services.Directory;
using System.Globalization;

namespace Grand.Plugin.Api.Extended.Controllers
{
    public class AddProductToCartCatalogCommand
    {
        public string ProductId { get; set; }
        public int ShoppingCartTypeId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateCartItemCommand
    {
        public string CartItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class EstimateShippingCommand
    {
        public string CountryId;
        public string StateProvinceId;
        public string ZipPostalCode;
    }

    public class UpdateCartCommand
    {
        public List<UpdateCartItemCommand> Commands { get; set; }
    }

    public class MigrateFromOfflineCartCommand
    {
        public List<AddProductToCartCatalogCommand> Commands { get; set; }
    }

    public class QuickAddProductToCartResponse
    {
        public IList<string> Warnings { get; set; }

        public string Redirect { get; set; }

        public int TotalCartItems { get; set; }

        public AddToCartModel AddtoCartModel { get; set; }

        public IList<ShoppingCartItem> CartItems { get; set; }
    }

    public class MigrateFromOfflineCartResponse
    {
        public ShoppingCartModel CartModel { get; set; }

        public OrderTotalsModel OrderTotalsModel { get; set; }

        public IList<string> Errors { get; set; }

        public IList<string> Warnings { get; set; }
    }

    public class GetCartResponse
    {
        public ShoppingCartModel CartModel;

        public OrderTotalsModel OrderTotalsModel { get; set; }
    }

    public class UpdateCartResponse
    {
        public int TotalProducts { get; set; }
        public ShoppingCartModel CartModel { get; set; }
    }

    [Route("api/mobile-cart")]
    public class MobileShoppingCartApiController : BaseApiController
    {
        #region Fields

        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly ILocalizationService _localizationService;
        private readonly IDiscountService _discountService;
        private readonly ICustomerService _customerService;
        private readonly IGiftCardService _giftCardService;
        private readonly ICheckoutAttributeService _checkoutAttributeService;
        private readonly IPermissionService _permissionService;
        private readonly IDownloadService _downloadService;
        private readonly IShoppingCartViewModelService _shoppingCartViewModelService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IProductService _productService;
        private readonly IProductReservationService _productReservationService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly ICurrencyService _currencyService;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly OrderSettings _orderSettings;

        #endregion

        #region Constructors

        public MobileShoppingCartApiController(
            IStoreContext storeContext,
            IWorkContext workContext,
            IShoppingCartService shoppingCartService,
            ILocalizationService localizationService,
            IDiscountService discountService,
            ICustomerService customerService,
            IGiftCardService giftCardService,
            ICheckoutAttributeService checkoutAttributeService,
            IPermissionService permissionService,
            IDownloadService downloadService,
            IShoppingCartViewModelService shoppingCartViewModelService,
            IGenericAttributeService genericAttributeService,
            IProductService productService,
            IProductReservationService productReservationService,
            ICustomerActivityService customerActivityService,
            ICurrencyService currencyService,
            ShoppingCartSettings shoppingCartSettings,
            OrderSettings orderSettings)
        {
            _workContext = workContext;
            _storeContext = storeContext;
            _shoppingCartService = shoppingCartService;
            _localizationService = localizationService;
            _discountService = discountService;
            _customerService = customerService;
            _giftCardService = giftCardService;
            _checkoutAttributeService = checkoutAttributeService;
            _permissionService = permissionService;
            _downloadService = downloadService;
            _shoppingCartViewModelService = shoppingCartViewModelService;
            _genericAttributeService = genericAttributeService;
            _productService = productService;
            _productReservationService = productReservationService;
            _customerActivityService = customerActivityService;
            _currencyService = currencyService;
            _shoppingCartSettings = shoppingCartSettings;
            _orderSettings = orderSettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Get shopping cart details
        /// </summary>
        /// <param name="checkoutAttributes"></param>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(ApiResponse<GetCartResponse>))]
        public virtual async Task<IActionResult> GetCart([FromQuery] bool validateCheckoutAttributes)
        {
            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            if(!cart.Any())
            {
                return Ok(ApiResponse<GetCartResponse>.FailResult("ShoppingCart.GetShoppingCart.CartIsEmpty"));
            }
            
            var model = new ShoppingCartModel();
            
            await _shoppingCartViewModelService.PrepareShoppingCart(model, cart, validateCheckoutAttributes: validateCheckoutAttributes);
            
            var orderTotalsModel = await _shoppingCartViewModelService.PrepareOrderTotals(cart, isEditable: false);

            return Ok(ApiResponse<GetCartResponse>.SuccessResult(new GetCartResponse {
                CartModel = model,
                OrderTotalsModel = orderTotalsModel
            }));
        }

        private async Task<ApiResponse<QuickAddProductToCartResponse>> QuickAddProductToCartInternal(AddProductToCartCatalogCommand command)
        {
            var productId = command.ProductId;
            var shoppingCartTypeId = command.ShoppingCartTypeId;
            var quantity = command.Quantity;

            var cartType = (ShoppingCartType)shoppingCartTypeId;

            var product = await _productService.GetProductById(productId);
            if (product == null)
            {
                return ApiResponse<QuickAddProductToCartResponse>.FailResult("ShoppingCart.QuickAddProductToCart.NoProductFound");
            }

            //we can add only simple products and 
            if (product.ProductType != ProductType.SimpleProduct || _shoppingCartSettings.AllowToSelectWarehouse)
            {
                return ApiResponse<QuickAddProductToCartResponse>.FailResult("ShoppingCart.QuickAddProductToCart.ProductTypeIsNotSimpleProduct");
            }

            //products with "minimum order quantity" more than a specified qty
            if (product.OrderMinimumQuantity > quantity)
            {
                //we cannot add to the cart such products from category pages
                //it can confuse customers. That's why we redirect customers to the product details page
                return ApiResponse<QuickAddProductToCartResponse>.FailResult("ShoppingCart.QuickAddProductToCart.MinimumOrderQuantityIsGreaterThanQuantity");
            }

            if (product.CustomerEntersPrice)
            {
                return ApiResponse<QuickAddProductToCartResponse>.FailResult("ShoppingCart.QuickAddProductToCart.ProductRequireCustomerEntersPrice");
            }

            var allowedQuantities = product.ParseAllowedQuantities();
            if (allowedQuantities.Length > 0)
            {
                //cannot be added to the cart (requires a customer to select a quantity from dropdownlist)
                return ApiResponse<QuickAddProductToCartResponse>.FailResult("ShoppingCart.QuickAddProductToCart.RequiresACustomerToSelectAQuantityFromDropdownlist");
            }

            if (product.ProductAttributeMappings.Any())
            {
                return ApiResponse<QuickAddProductToCartResponse>.FailResult("ShoppingCart.QuickAddProductToCart.ProductAttributeMappings");
            }

            var customer = _workContext.CurrentCustomer;

            string warehouseId =
               product.UseMultipleWarehouses ? _storeContext.CurrentStore.DefaultWarehouseId :
               (string.IsNullOrEmpty(_storeContext.CurrentStore.DefaultWarehouseId) ? product.WarehouseId : _storeContext.CurrentStore.DefaultWarehouseId);

            //get standard warnings without attribute validations
            //first, try to find existing shopping cart item
            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, cartType);
            var shoppingCartItem = await _shoppingCartService.FindShoppingCartItemInTheCart(cart, cartType, product.Id, warehouseId);
            //if we already have the same product in the cart, then use the total quantity to validate
            var quantityToValidate = shoppingCartItem != null ? shoppingCartItem.Quantity + quantity : quantity;
            var addToCartWarnings = await _shoppingCartService
                .GetShoppingCartItemWarnings(customer, new ShoppingCartItem() {
                    ShoppingCartType = cartType,
                    StoreId = _storeContext.CurrentStore.Id,
                    CustomerEnteredPrice = decimal.Zero,
                    WarehouseId = warehouseId,
                    Quantity = quantityToValidate
                },
                product, false);
            if (addToCartWarnings.Any())
            {
                //cannot be added to the cart
                return ApiResponse<QuickAddProductToCartResponse>.FailResult("ShoppingCart.QuickAddProductToCart.AddToCartWarnings",
                    new QuickAddProductToCartResponse { Warnings = addToCartWarnings });
            }

            //now let's try adding product to the cart (now including product attribute validation, etc)
            addToCartWarnings = await _shoppingCartService.AddToCart(customer: customer,
                productId: productId,
                shoppingCartType: cartType,
                storeId: _storeContext.CurrentStore.Id,
                warehouseId: warehouseId,
                quantity: quantity);
            if (addToCartWarnings.Any())
            {
                //cannot be added to the cart
                //but we do not display attribute and gift card warnings here. let's do it on the product details page
                return ApiResponse<QuickAddProductToCartResponse>.FailResult("ShoppingCart.QuickAddProductToCart.AddToCartWarnings",
                    new QuickAddProductToCartResponse { Warnings = addToCartWarnings });
            }

            var addtoCartModel = await _shoppingCartViewModelService.PrepareAddToCartModel(product, customer, quantity, 0, "", cartType, null, null, "", "", "");

            //added to the cart/wishlist
            switch (cartType)
            {
                case ShoppingCartType.Wishlist:
                    {
                        //activity log
                        await _customerActivityService.InsertActivity("PublicStore.AddToWishlist", product.Id, _localizationService.GetResource("ActivityLog.PublicStore.AddToWishlist"), product.Name);

                        if (_shoppingCartSettings.DisplayWishlistAfterAddingProduct)
                        {
                            //redirect to the wishlist page
                            return ApiResponse<QuickAddProductToCartResponse>.RedirectResult(new QuickAddProductToCartResponse { Redirect = "Wishlist" });
                        }

                        var cartItems = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.Wishlist);

                        return ApiResponse<QuickAddProductToCartResponse>.SuccessResult(new QuickAddProductToCartResponse {
                            TotalCartItems = cartItems.Sum(x => x.Quantity),
                            AddtoCartModel = addtoCartModel,
                            CartItems = cartItems
                        });
                    }
                case ShoppingCartType.ShoppingCart:
                default:
                    {
                        //activity log
                        await _customerActivityService.InsertActivity("PublicStore.AddToShoppingCart", product.Id, _localizationService.GetResource("ActivityLog.PublicStore.AddToShoppingCart"), product.Name);

                        var cartItems = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart);

                        return ApiResponse<QuickAddProductToCartResponse>.SuccessResult(new QuickAddProductToCartResponse {
                            TotalCartItems = cartItems.Sum(x => x.Quantity),
                            AddtoCartModel = addtoCartModel,
                            CartItems = cartItems
                        });
                    }
            }
        }

        /// <summary>
        /// Quick add product to cart which does not require much of product details
        /// Similar as AddToCartController.AddProductToCart_Catalog
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("quick-add-product-to-cart")]
        public virtual async Task<IActionResult> QuickAddProductToCart([FromBody] AddProductToCartCatalogCommand command)
        {
            return Ok(await QuickAddProductToCartInternal(command));
        }

        /// <summary>
        /// Add product to cart from product details page which is used to add product required customer input such as
        /// not simple product, user specifies price, custom attributes
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="shoppingCartTypeId"></param>
        /// <param name="form"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("add-product-details-to-cart/{productId}/{shoppingCartTypeId}")]
        public virtual async Task<IActionResult> AddProductDetailsToCart([FromRoute] string productId, [FromRoute] int shoppingCartTypeId, IFormCollection form)
        {
            var product = await _productService.GetProductById(productId);
            if (product == null)
            {
                return Ok(ApiResponse<string>.FailResult("ShoppingCart.AddProductDetailsToCart.NoProductFound"));
            }

            //you can't add group products
            if (product.ProductType == ProductType.GroupedProduct)
            {
                return Ok(ApiResponse<string>.FailResult("ShoppingCart.AddProductDetailsToCart.GroupedProductsCouldNotBeAddedToTheCart"));
            }

            //you can't add reservation product to wishlist
            if (product.ProductType == ProductType.Reservation && (ShoppingCartType)shoppingCartTypeId == ShoppingCartType.Wishlist)
            {
                return Ok(ApiResponse<string>.FailResult("ShoppingCart.AddProductDetailsToCart.ReservationProductsCouldNotBeAddedToTheWishlist"));
            }

            //you can't add auction product to wishlist
            if (product.ProductType == ProductType.Auction && (ShoppingCartType)shoppingCartTypeId == ShoppingCartType.Wishlist)
            {
                return Ok(ApiResponse<string>.FailResult("ShoppingCart.AddProductDetailsToCart.AuctionProductsCouldNotBeAddedToTheWishlist"));
            }

            #region Update existing shopping cart item?
            string updatecartitemid = "";
            foreach (string formKey in form.Keys)
            {
                if (formKey.Equals(string.Format("addtocart_{0}.UpdatedShoppingCartItemId", productId), StringComparison.OrdinalIgnoreCase))
                {
                    updatecartitemid = form[formKey];
                    break;
                }
            }

            ShoppingCartItem updatecartitem = null;
            if (_shoppingCartSettings.AllowCartItemEditing && !string.IsNullOrEmpty(updatecartitemid))
            {
                var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, (ShoppingCartType)shoppingCartTypeId);
                updatecartitem = cart.FirstOrDefault(x => x.Id == updatecartitemid);

                //is it this product?
                if (updatecartitem != null && product.Id != updatecartitem.ProductId)
                {
                    return Ok(ApiResponse<string>.FailResult("ShoppingCart.AddProductDetailsToCart.ThisProductDoesNotMatchAPsssedShoppingCartItemIdentifier"));
                }
            }
            #endregion

            #region Customer entered price
            decimal customerEnteredPriceConverted = decimal.Zero;
            if (product.CustomerEntersPrice)
            {
                foreach (string formKey in form.Keys)
                {
                    if (formKey.Equals(string.Format("addtocart_{0}.CustomerEnteredPrice", productId), StringComparison.OrdinalIgnoreCase))
                    {
                        if (decimal.TryParse(form[formKey], out decimal customerEnteredPrice))
                            customerEnteredPriceConverted = await _currencyService.ConvertToPrimaryStoreCurrency(customerEnteredPrice, _workContext.WorkingCurrency);
                        break;
                    }
                }
            }
            #endregion

            #region Quantity

            int quantity = 1;
            foreach (string formKey in form.Keys)
            {
                if (formKey.Equals(string.Format("addtocart_{0}.EnteredQuantity", productId), StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(form[formKey], out quantity);
                    break;
                }
            }

            #endregion

            //product and gift card attributes
            string attributes = await _shoppingCartViewModelService.ParseProductAttributes(product, form);

            //rental attributes
            DateTime? rentalStartDate = null;
            DateTime? rentalEndDate = null;
            if (product.ProductType == ProductType.Reservation)
            {
                _shoppingCartViewModelService.ParseReservationDates(product, form, out rentalStartDate, out rentalEndDate);
            }

            //product reservation
            string reservationId = "";
            string parameter = "";
            string duration = "";
            if (product.ProductType == ProductType.Reservation)
            {
                foreach (string formKey in form.Keys)
                {
                    if (formKey.Contains("Reservation"))
                    {
                        reservationId = form["Reservation"].ToString();
                        break;
                    }
                }

                if (product.IntervalUnitType == IntervalUnit.Hour || product.IntervalUnitType == IntervalUnit.Minute)
                {
                    if (string.IsNullOrEmpty(reservationId))
                    {
                        return Ok(ApiResponse<string>.FailResult("Product.Addtocart.Reservation.Required"));
                    }
                    var reservation = await _productReservationService.GetProductReservation(reservationId);
                    if (reservation == null)
                    {
                        return Ok(ApiResponse<string>.FailResult("Product.Addtocart.Reservation.NoReservationFound"));
                    }
                    duration = reservation.Duration;
                    rentalStartDate = reservation.Date;
                    parameter = reservation.Parameter;
                }
                else if (product.IntervalUnitType == IntervalUnit.Day)
                {
                    string datefrom = "";
                    string dateto = "";
                    foreach (var item in form)
                    {
                        if (item.Key == "reservationDatepickerFrom")
                        {
                            datefrom = item.Value;
                        }

                        if (item.Key == "reservationDatepickerTo")
                        {
                            dateto = item.Value;
                        }
                    }

                    string datePickerFormat = "MM/dd/yyyy";
                    if (!string.IsNullOrEmpty(datefrom))
                    {
                        rentalStartDate = DateTime.ParseExact(datefrom, datePickerFormat, CultureInfo.InvariantCulture);
                    }

                    if (!string.IsNullOrEmpty(dateto))
                    {
                        rentalEndDate = DateTime.ParseExact(dateto, datePickerFormat, CultureInfo.InvariantCulture);
                    }
                }
            }

            var cartType = updatecartitem == null ? (ShoppingCartType)shoppingCartTypeId :
                        //if the item to update is found, then we ignore the specified "shoppingCartTypeId" parameter
                        updatecartitem.ShoppingCartType;

            //save item
            var addToCartWarnings = new List<string>();

            if (product.AvailableEndDateTimeUtc.HasValue && product.AvailableEndDateTimeUtc.Value < DateTime.UtcNow)
            {
                return Ok(ApiResponse<string>.FailResult("ShoppingCart.NotAvailable"));
            }

            string warehouseId = _shoppingCartSettings.AllowToSelectWarehouse ?
                form["WarehouseId"].ToString() :
                product.UseMultipleWarehouses ? _storeContext.CurrentStore.DefaultWarehouseId :
                (string.IsNullOrEmpty(_storeContext.CurrentStore.DefaultWarehouseId) ? product.WarehouseId : _storeContext.CurrentStore.DefaultWarehouseId);

            if (updatecartitem == null)
            {
                //add to the cart
                addToCartWarnings.AddRange(await _shoppingCartService.AddToCart(_workContext.CurrentCustomer,
                    productId, cartType, _storeContext.CurrentStore.Id, warehouseId,
                    attributes, customerEnteredPriceConverted,
                    rentalStartDate, rentalEndDate, quantity, true, reservationId, parameter, duration));
            }
            else
            {
                var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, (ShoppingCartType)shoppingCartTypeId);
                var otherCartItemWithSameParameters = await _shoppingCartService.FindShoppingCartItemInTheCart(
                    cart, updatecartitem.ShoppingCartType, productId, warehouseId, attributes, customerEnteredPriceConverted,
                    rentalStartDate, rentalEndDate);
                if (otherCartItemWithSameParameters != null &&
                    otherCartItemWithSameParameters.Id == updatecartitem.Id)
                {
                    //ensure it's other shopping cart cart item
                    otherCartItemWithSameParameters = null;
                }
                //update existing item
                addToCartWarnings.AddRange(await _shoppingCartService.UpdateShoppingCartItem(_workContext.CurrentCustomer,
                    updatecartitem.Id, warehouseId, attributes, customerEnteredPriceConverted,
                    rentalStartDate, rentalEndDate, quantity, true));
                if (otherCartItemWithSameParameters != null && !addToCartWarnings.Any())
                {
                    //delete the same shopping cart item (the other one)
                    await _shoppingCartService.DeleteShoppingCartItem(_workContext.CurrentCustomer, otherCartItemWithSameParameters);
                }
            }

            #region Return result

            if (addToCartWarnings.Any())
            {
                //cannot be added to the cart/wishlist
                //let's display warnings
                return Ok(ApiResponse<List<string>>.FailResult("ShoppingCart.CannotBeAddedToTheCart", addToCartWarnings));
            }

            var addtoCartModel = await _shoppingCartViewModelService.PrepareAddToCartModel(product, _workContext.CurrentCustomer, quantity, customerEnteredPriceConverted, attributes, cartType, rentalStartDate, rentalEndDate, reservationId, parameter, duration);

            //added to the cart/wishlist
            switch (cartType)
            {
                case ShoppingCartType.Wishlist:
                    {
                        //activity log
                        await _customerActivityService.InsertActivity("PublicStore.AddToWishlist", product.Id, _localizationService.GetResource("ActivityLog.PublicStore.AddToWishlist"), product.Name);

                        if (_shoppingCartSettings.DisplayWishlistAfterAddingProduct)
                        {
                            //redirect to the wishlist page
                            return Ok(ApiResponse<string>.RedirectResult("Wishlist"));
                        }

                        var cartWishlistItems = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.Wishlist);
                        var totalCartWishlistItems = cartWishlistItems.Sum(x => x.Quantity);

                        return Ok(ApiResponse<dynamic>.SuccessResult(new
                        {
                            message = "Products.ProductHasBeenAddedToTheWishlist.Link",
                            cartWishlistItems,
                            totalCartWishlistItems,
                            addtoCartModel
                        }));
                    }
                case ShoppingCartType.ShoppingCart:
                default:
                    {
                        //activity log
                        await _customerActivityService.InsertActivity("PublicStore.AddToShoppingCart", product.Id, _localizationService.GetResource("ActivityLog.PublicStore.AddToShoppingCart"), product.Name);

                        if (_shoppingCartSettings.DisplayCartAfterAddingProduct)
                        {
                            //redirect to the shopping cart page
                            return Ok(ApiResponse<string>.RedirectResult("ShoppingCart"));
                        }

                        var cartItems = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart);
                        var totalCartItems = cartItems.Sum(x => x.Quantity);

                        return Ok(ApiResponse<dynamic>.SuccessResult(new
                        {
                            message = "Products.ProductHasBeenAddedToTheCart.Link",
                            cartItems,
                            totalCartItems,
                            addtoCartModel
                        }));
                    }
            }

            #endregion
        }

        [HttpPost]
        [Route("migrate-from-offline-cart")]
        [ProducesResponseType(200, Type = typeof(ApiResponse<MigrateFromOfflineCartResponse>))]
        public virtual async Task<IActionResult> MigrateFromOfflineCart([FromBody] MigrateFromOfflineCartCommand command)
        {
            if(command == null || !command.Commands.Any())
            {
                return Ok(ApiResponse<MigrateFromOfflineCartResponse>.FailResult("ShoppingCart.MigrateFromOfflineCart.NoCartItems"));
            }

            var errors = new List<string>();

            var warnings = new List<string>();

            foreach (var item in command.Commands)
            {
                var addResult = await QuickAddProductToCartInternal(item);

                if(addResult.HasError)
                {
                    errors.Add(addResult.Message);
                }

                if (addResult.HasData && addResult.Data.Warnings != null)
                {
                    warnings.AddRange(addResult.Data.Warnings);
                }
            }

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart);

            var model = new ShoppingCartModel();

            await _shoppingCartViewModelService.PrepareShoppingCart(model, cart);

            var orderTotalsModel = await _shoppingCartViewModelService.PrepareOrderTotals(cart, isEditable: false);

            return Ok(ApiResponse<MigrateFromOfflineCartResponse>.SuccessResult(new MigrateFromOfflineCartResponse {
                CartModel = model,
                OrderTotalsModel = orderTotalsModel,
                Errors = errors,
                Warnings = warnings
            }));
        }


        [HttpPost]
        [Route("update-cart")]
        public virtual async Task<IActionResult> UpdateCart([FromBody] UpdateCartCommand command)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.EnableShoppingCart))
            {
                return Ok(ApiResponse<UpdateCartResponse>.FailResult("Unauthorize.StandardPermissionProvider.EnableShoppingCart"));
            }

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart);

            //current warnings <cart item identifier, warnings>
            var innerWarnings = new Dictionary<string, IList<string>>();
            foreach (var sci in cart)
            {
                foreach (var item in command.Commands)
                {
                    if (item.CartItemId.Equals(sci.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        var currSciWarnings = await _shoppingCartService.UpdateShoppingCartItem(_workContext.CurrentCustomer,
                                sci.Id, sci.WarehouseId, sci.AttributesXml, sci.CustomerEnteredPrice,
                                sci.RentalStartDateUtc, sci.RentalEndDateUtc,
                                item.Quantity, true, sci.ReservationId, sci.Id);
                        innerWarnings.Add(sci.Id, currSciWarnings);
                        break;
                    }
                }
            }

            //updated cart
            _workContext.CurrentCustomer = await _customerService.GetCustomerById(_workContext.CurrentCustomer.Id);
            cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            var model = new ShoppingCartModel();
            await _shoppingCartViewModelService.PrepareShoppingCart(model, cart);
            //update current warnings
            foreach (var kvp in innerWarnings)
            {
                //kvp = <cart item identifier, warnings>
                var sciId = kvp.Key;
                var warnings = kvp.Value;
                //find model
                var sciModel = model.Items.FirstOrDefault(x => x.Id == sciId);
                if (sciModel != null)
                    foreach (var w in warnings)
                        if (!sciModel.Warnings.Contains(w))
                            sciModel.Warnings.Add(w);
            }

            return Ok(ApiResponse<UpdateCartResponse>.SuccessResult(new UpdateCartResponse {
                TotalProducts = model.Items.Sum(x => x.Quantity),
                CartModel = model
            }));
        }

        [HttpDelete]
        [Route("clear-cart")]
        public virtual async Task<IActionResult> ClearCart()
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.EnableShoppingCart))
            {
                return Ok(ApiResponse<string>.FailResult("Unauthorize.StandardPermissionProvider.EnableShoppingCart"));
            }

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            foreach (var sci in cart)
            {
                await _shoppingCartService.DeleteShoppingCartItem(_workContext.CurrentCustomer, sci, ensureOnlyActiveCheckoutAttributes: true);
            }

            return Ok(ApiResponse<string>.SuccessResult("ShoppingCart.ClearCart.Success"));
        }

        [HttpDelete]
        [Route("delete-cart-item/{id}")]
        public virtual async Task<IActionResult> DeleteCartItem([FromRoute] string id)
        {
            if (!await _permissionService.Authorize(StandardPermissionProvider.EnableShoppingCart))
            {
                return Ok(ApiResponse<UpdateCartResponse>.FailResult("Unauthorize.StandardPermissionProvider.EnableShoppingCart"));
            }

            var item = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart)
                .FirstOrDefault(sci => sci.Id == id);

            if (item != null)
            {
                await _shoppingCartService.DeleteShoppingCartItem(_workContext.CurrentCustomer, item, ensureOnlyActiveCheckoutAttributes: true);
            }

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            var shoppingcartmodel = new ShoppingCartModel();

            await _shoppingCartViewModelService.PrepareShoppingCart(shoppingcartmodel, cart);

            return Ok(ApiResponse<UpdateCartResponse>.SuccessResult(new UpdateCartResponse {
                TotalProducts = shoppingcartmodel.Items.Sum(x => x.Quantity),
                CartModel = shoppingcartmodel
            }));
        }

        [HttpPost]
        [Route("apply-discount-coupon/{discountcouponcode}")]
        public virtual async Task<IActionResult> ApplyDiscountCoupon([FromRoute] string discountcouponcode)
        {
            if(string.IsNullOrWhiteSpace(discountcouponcode))
            {
                return Ok(ApiResponse<ShoppingCartModel>.FailResult("ShoppingCart.ApplyDiscountCoupon.discountcouponcode.required"));
            }

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            var model = new ShoppingCartModel();
            if (!string.IsNullOrWhiteSpace(discountcouponcode))
            {
                discountcouponcode = discountcouponcode.ToUpper();
                //we find even hidden records here. this way we can display a user-friendly message if it's expired
                var discount = await _discountService.GetDiscountByCouponCode(discountcouponcode, true);
                if (discount != null && discount.RequiresCouponCode)
                {
                    var coupons = await _workContext.CurrentCustomer.ParseAppliedDiscountCouponCodes(_genericAttributeService);
                    var existsAndUsed = false;
                    foreach (var item in coupons)
                    {
                        if (await _discountService.ExistsCodeInDiscount(item, discount.Id, null))
                            existsAndUsed = true;
                    }
                    if (!existsAndUsed)
                    {
                        if (!discount.Reused)
                            existsAndUsed = !await _discountService.ExistsCodeInDiscount(discountcouponcode, discount.Id, false);

                        if (!existsAndUsed)
                        {
                            var validationResult = await _discountService.ValidateDiscount(discount, _workContext.CurrentCustomer, discountcouponcode);
                            if (validationResult.IsValid)
                            {
                                //valid
                                await _workContext.CurrentCustomer.ApplyDiscountCouponCode(_genericAttributeService, discountcouponcode);
                                model.DiscountBox.Message = _localizationService.GetResource("ShoppingCart.DiscountCouponCode.Applied");
                                model.DiscountBox.IsApplied = true;
                            }
                            else
                            {
                                if (!String.IsNullOrEmpty(validationResult.UserError))
                                {
                                    //some user error
                                    model.DiscountBox.Message = validationResult.UserError;
                                    model.DiscountBox.IsApplied = false;
                                }
                                else
                                {
                                    //general error text
                                    model.DiscountBox.Message = _localizationService.GetResource("ShoppingCart.DiscountCouponCode.WrongDiscount");
                                    model.DiscountBox.IsApplied = false;
                                }
                            }
                        }
                        else
                        {
                            model.DiscountBox.Message = _localizationService.GetResource("ShoppingCart.DiscountCouponCode.WasUsed");
                            model.DiscountBox.IsApplied = false;
                        }
                    }
                    else
                    {
                        model.DiscountBox.Message = _localizationService.GetResource("ShoppingCart.DiscountCouponCode.UsesTheSameDiscount");
                        model.DiscountBox.IsApplied = false;
                    }
                }
                else
                {
                    model.DiscountBox.Message = _localizationService.GetResource("ShoppingCart.DiscountCouponCode.WrongDiscount");
                    model.DiscountBox.IsApplied = false;
                }
            }
            else
            {
                model.DiscountBox.Message = _localizationService.GetResource("ShoppingCart.DiscountCouponCode.Required");
                model.DiscountBox.IsApplied = false;
            }

            await _shoppingCartViewModelService.PrepareShoppingCart(model, cart);

            return Ok(ApiResponse<ShoppingCartModel>.SuccessResult(model));
        }


        [HttpPost]
        [Route("apply-gift-card/{giftcardcouponcode}")]
        public virtual async Task<IActionResult> ApplyGiftCard([FromRoute] string giftcardcouponcode)
        {
            if(string.IsNullOrWhiteSpace(giftcardcouponcode))
            {
                return Ok(ApiResponse<ShoppingCartModel>.FailResult("ShoppingCart.ApplyGiftCard.giftcardcouponcode.required"));
            }

            //trim
            if (giftcardcouponcode != null)
                giftcardcouponcode = giftcardcouponcode.Trim();

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            var model = new ShoppingCartModel();
            if (!cart.IsRecurring())
            {
                if (!string.IsNullOrWhiteSpace(giftcardcouponcode))
                {
                    var giftCard = (await _giftCardService.GetAllGiftCards(giftCardCouponCode: giftcardcouponcode)).FirstOrDefault();
                    bool isGiftCardValid = giftCard != null && giftCard.IsGiftCardValid();
                    if (isGiftCardValid)
                    {
                        await _workContext.CurrentCustomer.ApplyGiftCardCouponCode(_genericAttributeService, giftcardcouponcode);
                        model.GiftCardBox.Message = _localizationService.GetResource("ShoppingCart.GiftCardCouponCode.Applied");
                        model.GiftCardBox.IsApplied = true;
                    }
                    else
                    {
                        model.GiftCardBox.Message = _localizationService.GetResource("ShoppingCart.GiftCardCouponCode.WrongGiftCard");
                        model.GiftCardBox.IsApplied = false;
                    }
                }
                else
                {
                    model.GiftCardBox.Message = _localizationService.GetResource("ShoppingCart.GiftCardCouponCode.Required");
                    model.GiftCardBox.IsApplied = false;
                }
            }
            else
            {
                model.GiftCardBox.Message = _localizationService.GetResource("ShoppingCart.GiftCardCouponCode.DontWorkWithAutoshipProducts");
                model.GiftCardBox.IsApplied = false;
            }

            await _shoppingCartViewModelService.PrepareShoppingCart(model, cart);

            return Ok(ApiResponse<ShoppingCartModel>.SuccessResult(model));
        }

        /// <summary>
        /// Request payload is multipart/form-data with following fields
        /// CountryId, StateProvinceId, ZipPostalCode
        /// checkout_attribute_{attribute.Id}=5e3c32c8504e9c00e842477d
        /// </summary>
        /// <param name="form"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("get-estimate-shipping")]
        public virtual async Task<IActionResult> GetEstimateShipping(IFormCollection form)
        {
            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            //parse and save checkout attributes , IFormCollection form
            await _shoppingCartViewModelService.ParseAndSaveCheckoutAttributes(cart, form);

            var countryId = form["CountryId"];
            var stateProvinceId = form["StateProvinceId"];
            var zipPostalCode = form["ZipPostalCode"];

            var model = await _shoppingCartViewModelService.PrepareEstimateShippingResult(cart, countryId, stateProvinceId, zipPostalCode);

            return Ok(ApiResponse<EstimateShippingResultModel>.SuccessResult(model));
        }

        [HttpPost]
        [Route("remove-discount-coupon/{discountId}")]
        public virtual async Task<IActionResult> RemoveDiscountCoupon([FromRoute] string discountId)
        {
            if (string.IsNullOrWhiteSpace(discountId))
            {
                return Ok(ApiResponse<string>.FailResult("ShoppingCart.RemoveDiscountCoupon.discountId.required"));
            }

            var model = new ShoppingCartModel();
            var discount = await _discountService.GetDiscountById(discountId);
            if (discount != null)
            {
                var coupons = await _workContext.CurrentCustomer.ParseAppliedDiscountCouponCodes(_genericAttributeService);
                foreach (var item in coupons)
                {
                    var dd = await _discountService.GetDiscountByCouponCode(item);
                    if (dd.Id == discount.Id)
                        await _workContext.CurrentCustomer.RemoveDiscountCouponCode(_genericAttributeService, item);
                }
            }

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            await _shoppingCartViewModelService.PrepareShoppingCart(model, cart);

            return Ok(ApiResponse<ShoppingCartModel>.SuccessResult(model));
        }

        [HttpPost]
        [Route("remove-gift-card-code/{giftCardId}")]
        public virtual async Task<IActionResult> RemoveGiftCardCode(string giftCardId)
        {
            if (string.IsNullOrWhiteSpace(giftCardId))
            {
                return Ok(ApiResponse<string>.FailResult("ShoppingCart.RemoveGiftCardCode.giftCardId.required"));
            }

            var model = new ShoppingCartModel();

            //get gift card identifier
            var gc = await _giftCardService.GetGiftCardById(giftCardId);
            if (gc != null)
            {
                await _workContext.CurrentCustomer.RemoveGiftCardCouponCode(_genericAttributeService, gc.GiftCardCouponCode);
            }

            var cart = _shoppingCartService.GetShoppingCart(_storeContext.CurrentStore.Id, ShoppingCartType.ShoppingCart, ShoppingCartType.Auctions);

            await _shoppingCartViewModelService.PrepareShoppingCart(model, cart);

            return Ok(ApiResponse<ShoppingCartModel>.SuccessResult(model));
        }

        #endregion
    }
}
