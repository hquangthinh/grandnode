using System.Collections.Generic;
using System.Threading.Tasks;
using Grand.Core;
using Grand.Core.Caching;
using Grand.Core.Domain.Blogs;
using Grand.Core.Domain.Catalog;
using Grand.Core.Domain.Common;
using Grand.Core.Domain.Forums;
using Grand.Core.Domain.Media;
using Grand.Core.Domain.Vendors;
using Grand.Services.Blogs;
using Grand.Services.Catalog;
using Grand.Services.Common;
using Grand.Services.Customers;
using Grand.Services.Directory;
using Grand.Services.Localization;
using Grand.Services.Media;
using Grand.Services.Security;
using Grand.Services.Stores;
using Grand.Services.Topics;
using Grand.Services.Vendors;
using Grand.Web.Extensions;
using Grand.Web.Interfaces;
using Grand.Web.Models.Catalog;
using Grand.Web.Models.Media;

namespace Grand.Plugin.Api.Extended.Services
{
    public class DefaultMobileHomeViewModelService : IMobileHomeViewModelService
    {
        private const string CategoryMobileHomepageKey = "Grand.pres.category.mobile.homepage-{0}-{1}-{2}-{3}";
        
        private readonly IWebHelper _webHelper;
        private readonly IProductViewModelService _productViewModelService;
        private readonly ILocalizationService _localizationService;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly ICacheManager _cacheManager;
        private readonly ICategoryService _categoryService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IProductService _productService;
        private readonly IPictureService _pictureService;
        private readonly IProductTagService _productTagService;
        private readonly ICurrencyService _currencyService;
        private readonly ISearchTermService _searchTermService;
        private readonly IAclService _aclService;
        private readonly IStoreMappingService _storeMappingService;
        private readonly ISpecificationAttributeService _specificationAttributeService;
        private readonly ICategoryTemplateService _categoryTemplateService;
        private readonly IManufacturerTemplateService _manufacturerTemplateService;
        private readonly IPriceFormatter _priceFormatter;
        private readonly CatalogSettings _catalogSettings;
        private readonly MediaSettings _mediaSettings;
        private readonly VendorSettings _vendorSettings;

        public DefaultMobileHomeViewModelService(
            IWebHelper webHelper,
            IProductViewModelService productViewModelService,
            ILocalizationService localizationService,
            IWorkContext workContext,
            IStoreContext storeContext,
            ICacheManager cacheManager,
            ICategoryService categoryService,
            IManufacturerService manufacturerService,
            IProductService productService,
            ITopicService topicService,
            IPictureService pictureService,
            IVendorService vendorService,
            IProductTagService productTagService,
            ICurrencyService currencyService,
            ISearchTermService searchTermService,
            IAclService aclService,
            IStoreMappingService storeMappingService,
            ISpecificationAttributeService specificationAttributeService,
            ICategoryTemplateService categoryTemplateService,
            IManufacturerTemplateService manufacturerTemplateService,
            IPriceFormatter priceFormatter,
            IAddressViewModelService addressViewModelService,
            IBlogService blogService,
            CatalogSettings catalogSettings,
            BlogSettings blogSettings,
            ForumSettings forumSettings,
            MenuItemSettings menuItemSettings,
            MediaSettings mediaSettings,
            VendorSettings vendorSettings)
        {
            _webHelper = webHelper;
            _productViewModelService = productViewModelService;
            _localizationService = localizationService;
            _workContext = workContext;
            _storeContext = storeContext;
            _cacheManager = cacheManager;
            _categoryService = categoryService;
            _manufacturerService = manufacturerService;
            _productService = productService;
            _pictureService = pictureService;
            _productTagService = productTagService;
            _currencyService = currencyService;
            _searchTermService = searchTermService;
            _aclService = aclService;
            _storeMappingService = storeMappingService;
            _specificationAttributeService = specificationAttributeService;
            _categoryTemplateService = categoryTemplateService;
            _manufacturerTemplateService = manufacturerTemplateService;
            _priceFormatter = priceFormatter;
            _catalogSettings = catalogSettings;
            _mediaSettings = mediaSettings;
            _vendorSettings = vendorSettings;
        }
        
        public async Task<IList<CategoryModel>> GetAllActiveTopLevelCategories()
        {
            var categoriesCacheKey = string.Format(CategoryMobileHomepageKey,
                string.Join(",", _workContext.CurrentCustomer.GetCustomerRoleIds()),
                _storeContext.CurrentStore.Id,
                _workContext.WorkingLanguage.Id,
                _webHelper.GetMachineName());

            var model = await _cacheManager.GetAsync(categoriesCacheKey, async () =>
            {
                var cat = new List<CategoryModel>();
                foreach (var x in (await _categoryService.GetAllCategoriesByParentCategoryId()))
                {
                    var catModel = x.ToModel(_workContext.WorkingLanguage);
                    //prepare picture model
                    catModel.PictureModel = new PictureModel {
                        Id = x.PictureId,
                        FullSizeImageUrl = await _pictureService.GetPictureUrl(x.PictureId),
                        ImageUrl = await _pictureService.GetPictureUrl(x.PictureId, _mediaSettings.CategoryThumbPictureSize),
                        Title = string.Format(_localizationService.GetResource("Media.Category.ImageLinkTitleFormat"), catModel.Name),
                        AlternateText = string.Format(_localizationService.GetResource("Media.Category.ImageAlternateTextFormat"), catModel.Name)
                    };
                    cat.Add(catModel);
                }
                return cat;
            });

            return model;
        }
    }
}