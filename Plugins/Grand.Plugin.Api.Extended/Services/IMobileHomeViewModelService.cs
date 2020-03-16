using System.Collections.Generic;
using System.Threading.Tasks;
using Grand.Web.Models.Catalog;

namespace Grand.Plugin.Api.Extended.Services
{
    public interface IMobileHomeViewModelService
    {
        Task<IList<CategoryModel>> GetAllActiveTopLevelCategories();
    }
}