using System.Threading.Tasks;
using Grand.Services.Catalog;
using Grand.Web.Areas.Api.Controllers;
using Grand.Web.Interfaces;
using Grand.Web.Models.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Plugin.Api.Extended.Controllers
{
    [Route("api/mobile-product")]
    public class MobileProductApiController : BaseApiController
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

        [Route("{productId}/reviews")]
        [ProducesResponseType(200, Type = typeof(ProductReviewsModel))]
        public virtual async Task<IActionResult> GetReviewsForProduct([FromRoute] string productId, 
            [FromQuery] int page, [FromQuery]  int pageSize)
        {
            var model = new ProductReviewsModel();
            var product = await _productService.GetProductById(productId);
            if (product == null || !product.Published || !product.AllowCustomerReviews)
                return Ok(model);

            await _productViewModelService.PrepareProductReviewsModel(model, product, pageSize);

            return Ok(model);
        }

        #endregion
    }
}