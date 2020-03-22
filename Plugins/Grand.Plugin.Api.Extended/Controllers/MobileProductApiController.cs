using System.Threading.Tasks;
using Grand.Services.Catalog;
using Grand.Web.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Plugin.Api.Extended.Controllers
{
    [Route("api/mobile-product")]
    public class MobileProductApiController : BaseAuthorizedApiController
    {
        #region Fields
        
        private readonly IProductService _productService;
        private readonly IProductViewModelService _productViewModelService;

        #endregion
        
        #region Constructors

        public MobileProductApiController(
            IProductViewModelService productViewModelService, IProductService productService)
        {
            _productViewModelService = productViewModelService;
            _productService = productService;
        }

        #endregion
        
        #region Methods
        
        [Route("{productId}")]
        public virtual async Task<IActionResult> ProductDetails([FromRoute] string productId)
        {
            var product = await _productService.GetProductById(productId);
            if (product == null)
                return NotFound();
            
            //prepare the model
            var model = await _productViewModelService.PrepareProductDetailsPage(product, updatecartitem: null, false);

            return Ok(model);
        }
        
        #endregion
    }
}