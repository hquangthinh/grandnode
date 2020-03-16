using System.Linq;
using System.Threading.Tasks;
using Grand.Plugin.Api.Extended.DTOs;
using Grand.Plugin.Api.Extended.Services;
using Grand.Web.Areas.Api.Controllers;
using Grand.Web.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Plugin.Api.Extended.Controllers
{
    [Route("api/mobile-home")]
    public class MobileHomeApiController : BaseApiController
    {
        #region Fields

        private readonly ICatalogViewModelService _catalogViewModelService;
        private readonly IProductViewModelService _productViewModelService;
        private readonly IMobileHomeViewModelService _mobileHomeViewModelService;
        
        #endregion
        
        #region Constructors

        public MobileHomeApiController(
            ICatalogViewModelService catalogViewModelService, 
            IProductViewModelService productViewModelService, 
            IMobileHomeViewModelService mobileHomeViewModelService)
        {
            _catalogViewModelService = catalogViewModelService;
            _productViewModelService = productViewModelService;
            _mobileHomeViewModelService = mobileHomeViewModelService;
        }

        #endregion
        
        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetMobileHomeViewModel()
        {
            var homeVm = new MobileHomeViewModel {
                AllCategories = await _mobileHomeViewModelService.GetAllActiveTopLevelCategories(),
                PopularCategories = await _catalogViewModelService.PrepareHomepageCategory(),
                CategoriesHaveFeaturedProducts = await _catalogViewModelService.PrepareCategoryFeaturedProducts()
            };
            return Ok(homeVm);
        }
        
        /// <summary>
        /// All products that have ShowOnHomePage = true
        /// </summary>
        /// <param name="productThumbPictureSize"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("homepage-products")]
        public async Task<IActionResult> GetMobileHomePageProducts([FromQuery] int? productThumbPictureSize)
        {
            return Ok(await _productViewModelService.PrepareProductsDisplayedOnHomePage(productThumbPictureSize));
        }
        
        /// <summary>
        /// All products that markedAsNewOnly = true
        /// </summary>
        /// <param name="productThumbPictureSize"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("homepage-new-products")]
        public async Task<IActionResult> GetMobileHomePageNewProducts([FromQuery] int? productThumbPictureSize)
        {
            return Ok(await _productViewModelService.PrepareNewProductsDisplayedOnHomePage(productThumbPictureSize));
        }
    }
}