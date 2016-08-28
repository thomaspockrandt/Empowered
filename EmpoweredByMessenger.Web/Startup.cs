using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(EmpoweredByMessenger.Web.Startup))]

namespace EmpoweredByMessenger.Web
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
        }
    }
}
