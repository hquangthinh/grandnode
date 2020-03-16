using Autofac;
using Grand.Core.Configuration;
using Grand.Core.Infrastructure;
using Grand.Core.Infrastructure.DependencyManagement;
using Grand.Plugin.Api.Extended.Services;

namespace Grand.Plugin.Api.Extended
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, GrandConfig config)
        {
            builder.RegisterType<ApiPlugin>().InstancePerLifetimeScope();
            builder.RegisterType<DefaultMobileHomeViewModelService>().As<IMobileHomeViewModelService>().InstancePerLifetimeScope();
        }

        public int Order
        {
            get { return 10; }
        }
    }
}
