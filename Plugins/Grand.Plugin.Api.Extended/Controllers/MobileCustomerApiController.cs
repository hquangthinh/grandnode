using Grand.Core;
using Grand.Core.Domain.Customers;
using Grand.Web.Areas.Api.Controllers;
using Grand.Web.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;

namespace Grand.Plugin.Api.Extended.Controllers
{
	[Route("api/mobile-customer")]
	public class MobileCustomerApiController: BaseApiController
	{
		private readonly ICustomerViewModelService _customerViewModelService;
		IWorkContext _workContext;

		#region Constructor
		public MobileCustomerApiController(ICustomerViewModelService customerViewModelService,
			IWorkContext workContext)
		{
			_customerViewModelService = customerViewModelService;
			_workContext = workContext;

		}
		#endregion

		[Route("")]
		[HttpGet]
		public async Task<IActionResult> GetCurrentCustomerInfo()
		{
			return !_workContext.CurrentCustomer.IsRegistered() ? 
				Unauthorized(HttpStatusCode.Unauthorized) : 
				(IActionResult)Ok(_workContext.CurrentCustomer);
		}

	}
}