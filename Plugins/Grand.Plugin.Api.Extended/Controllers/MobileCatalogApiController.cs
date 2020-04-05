using System.Threading.Tasks;
using Grand.Services.Catalog;
using Grand.Web.Areas.Api.Controllers;
using Grand.Web.Interfaces;
using Grand.Web.Models.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Grand.Plugin.Api.Extended.Controllers
{
    [Route("api/mobile-catalog")]
    public class MobileCatalogApiController : BaseApiController
    {
        #region Fields

        private readonly ICatalogViewModelService _catalogViewModelService;

        #endregion

        #region Constructors

        public MobileCatalogApiController(ICatalogViewModelService catalogViewModelService)
        {
            _catalogViewModelService = catalogViewModelService;
        }

        #endregion

        #region Categories

        [Route("categories/{categoryId}")]
        [HttpPost]
        public virtual async Task<IActionResult> Category([FromRoute] string categoryId, [FromBody] CatalogPagingFilteringModel command)
        {
            var category = await _catalogViewModelService.GetCategoryById(categoryId);

            if (category == null)
                return Ok(new CategoryModel());

            var viewModel = await _catalogViewModelService.PrepareCategory(category, command);

            return Ok(viewModel);
        }

        #endregion
    }
}
