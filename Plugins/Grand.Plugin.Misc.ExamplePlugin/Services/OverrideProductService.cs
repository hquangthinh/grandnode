using System;
using System.Threading.Tasks;
using Grand.Core;
using Grand.Core.Caching;
using Grand.Core.Data;
using Grand.Core.Domain.Catalog;
using Grand.Core.Domain.Common;
using Grand.Core.Domain.Customers;
using Grand.Core.Domain.Localization;
using Grand.Core.Domain.Seo;
using Grand.Services.Catalog;
using Grand.Services.Messages;
using MediatR;

namespace Grand.Plugin.Misc.ExamplePlugin.Services
{
    public class OverrideProductService: ProductService
    {
        public OverrideProductService(ICacheManager cacheManager, IRepository<Product> productRepository,
            IRepository<ProductReview> productReviewRepository, IRepository<UrlRecord> urlRecordRepository,
            IRepository<Customer> customerRepository, IRepository<CustomerRoleProduct> customerRoleProductRepository,
            IRepository<CustomerTagProduct> customerTagProductRepository,
            IRepository<ProductDeleted> productDeletedRepository,
            IRepository<CustomerProduct> customerProductRepository, IProductAttributeService productAttributeService,
            IProductAttributeParser productAttributeParser,
            ISpecificationAttributeService specificationAttributeService,
            IWorkflowMessageService workflowMessageService, IWorkContext workContext,
            LocalizationSettings localizationSettings, CommonSettings commonSettings, CatalogSettings catalogSettings,
            IMediator mediator, IRepository<ProductTag> productTagRepository, IServiceProvider serviceProvider) : base(
            cacheManager, productRepository, productReviewRepository, urlRecordRepository, customerRepository,
            customerRoleProductRepository, customerTagProductRepository, productDeletedRepository,
            customerProductRepository, productAttributeService, productAttributeParser, specificationAttributeService,
            workflowMessageService, workContext, localizationSettings, commonSettings, catalogSettings, mediator,
            productTagRepository, serviceProvider)
        {
        }

        public override Task DeleteProduct(Product product)
        {
            throw new Exception("[OverrideProductService]: You cannot delete product");
        }
    }
}