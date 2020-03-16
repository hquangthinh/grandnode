using System.Collections.Generic;
using Grand.Web.Models.Catalog;

namespace Grand.Plugin.Api.Extended.DTOs
{
    public class MobileHomeViewModel
    {
        public MobileHomeViewModel()
        {
            AllCategories = new List<CategoryModel>();
            PopularCategories = new List<CategoryModel>();
            CategoriesHaveFeaturedProducts = new List<CategoryModel>();
        }
        
        /// <summary>
        /// All published categories
        /// </summary>
        public IList<CategoryModel> AllCategories { get; set; }
        
        /// <summary>
        /// All categories that have ShowOnHomePage = true
        /// </summary>
        public IList<CategoryModel> PopularCategories { get; set; }
        
        public IList<CategoryModel> CategoriesHaveFeaturedProducts { get; set; }
    }
}