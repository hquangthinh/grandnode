using System.Threading.Tasks;
using Grand.Core;
using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Orders;
using Grand.Services.Localization;
using Grand.Services.Orders;
using Grand.Services.Payments;
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
        private readonly OrderSettings _orderSettings;

        #endregion
        
        #region Constructors
        
        public MobileOrderApiController(IOrderViewModelService orderViewModelService,
            IOrderService orderService,
            IWorkContext workContext,
            IOrderProcessingService orderProcessingService,
            IPaymentService paymentService,
            ILocalizationService localizationService,
            OrderSettings orderSettings)
        {
            _orderViewModelService = orderViewModelService;
            _orderService = orderService;
            _workContext = workContext;
            _orderProcessingService = orderProcessingService;
            _paymentService = paymentService;
            _localizationService = localizationService;
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
        
        #endregion
    }
}