using System.Threading.Tasks;
using Grand.Core;
using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Orders;
using Grand.Core.Domain.Shipping;
using Grand.Services.Localization;
using Grand.Services.Orders;
using Grand.Services.Payments;
using Grand.Services.Shipping;
using Grand.Web.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Plugin.Api.Extended.Controllers
{
    [Route("api/mobile-order")]
    public class MobileOrderApiController : BaseAuthorizedApiController
    {
        #region Fields

        private readonly IOrderViewModelService _orderViewModelService;
        private readonly IOrderService _orderService;
        private readonly IWorkContext _workContext;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPaymentService _paymentService;
        private readonly ILocalizationService _localizationService;
        private readonly IShipmentService _shipmentService;
        private readonly OrderSettings _orderSettings;

        #endregion

        #region Constructors

        public MobileOrderApiController(IOrderViewModelService orderViewModelService,
            IOrderService orderService,
            IWorkContext workContext,
            IOrderProcessingService orderProcessingService,
            IPaymentService paymentService,
            ILocalizationService localizationService,
            IShipmentService shipmentService,
            OrderSettings orderSettings)
        {
            _orderViewModelService = orderViewModelService;
            _orderService = orderService;
            _workContext = workContext;
            _orderProcessingService = orderProcessingService;
            _paymentService = paymentService;
            _localizationService = localizationService;
            _shipmentService = shipmentService;
            _orderSettings = orderSettings;
        }

        #endregion

        #region Methods

        [Route("my-orders")]
        public virtual async Task<IActionResult> CustomerOrders()
        {
            if (!_workContext.CurrentCustomer.IsRegistered())
                return Unauthorized();

            var model = await _orderViewModelService.PrepareCustomerOrderList();

            return Ok(model);
        }

        [Route("{orderId}")]
        public virtual async Task<IActionResult> Details([FromRoute] string orderId)
        {
            var order = await _orderService.GetOrderById(orderId);

            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return Unauthorized();

            var model = await _orderViewModelService.PrepareOrderDetails(order);

            return Ok(model);
        }

        [HttpPost]
        [Route("cancel/{orderId}")]
        public virtual async Task<IActionResult> CancelOrder([FromRoute] string orderId)
        {
            var order = await _orderService.GetOrderById(orderId);

            if (order == null || order.PaymentStatus != Core.Domain.Payments.PaymentStatus.Pending
                || (order.ShippingStatus != ShippingStatus.ShippingNotRequired && order.ShippingStatus != ShippingStatus.NotYetShipped)
                || order.OrderStatus != OrderStatus.Pending
                || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId
                || !_orderSettings.UserCanCancelUnpaidOrder)
            {
                return Unauthorized();
            }

            await _orderProcessingService.CancelOrder(order, true, true);

            return Ok(new { success = true, orderId });
        }

        [HttpPost]
        [Route("reorder/{orderId}")]
        public virtual async Task<IActionResult> ReOrder([FromRoute] string orderId)
        {
            var order = await _orderService.GetOrderById(orderId);

            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
            {
                return Unauthorized();
            }

            await _orderProcessingService.ReOrder(order);

            return Ok(new { success = true, order });
        }

        [HttpGet]
        [Route("shipment-details/{shipmentId}")]
        public virtual async Task<IActionResult> ShipmentDetails(string shipmentId)
        {
            var shipment = await _shipmentService.GetShipmentById(shipmentId);

            if (shipment == null)
                return Unauthorized();

            var order = await _orderService.GetOrderById(shipment.OrderId);

            if (order == null || order.Deleted || _workContext.CurrentCustomer.Id != order.CustomerId)
                return Unauthorized();

            var model = await _orderViewModelService.PrepareShipmentDetails(shipment);

            return Ok(model);
        }

        #endregion
    }
}