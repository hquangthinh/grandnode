using Grand.Framework.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Grand.Plugin.Misc.ExamplePlugin
{
    public class AdminTabsExample : INotificationHandler<AdminTabStripCreated>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminTabsExample(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Task Handle(AdminTabStripCreated eventMessage, CancellationToken cancellationToken)
        {
            if (eventMessage.TabStripName == "category-edit")
            {
                var categoryId = Convert.ToString(_httpContextAccessor.HttpContext.GetRouteValue("ID"));
                eventMessage.BlocksToRender.Add(("test new tab", new Microsoft.AspNetCore.Html.HtmlString($"<div>TEST {categoryId}</div>")));
            }
            return Task.CompletedTask;
        }
    }
}
