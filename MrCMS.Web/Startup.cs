﻿using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using MrCMS.Apps;
using MrCMS.DbConfiguration;
using MrCMS.Entities.Documents.Web;
using MrCMS.Entities.People;
using MrCMS.Helpers;
using MrCMS.Services;
using MrCMS.Services.Resources;
using MrCMS.Settings;
using MrCMS.Web.Apps.Admin;
using MrCMS.Web.Apps.Admin.Filters;
using MrCMS.Web.Apps.Core;
using MrCMS.Website;
using MrCMS.Website.CMS;
using NHibernate;
using ISession = NHibernate.ISession;

namespace MrCMS.Web
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;

            TypeHelper.Initialize(GetType().Assembly);
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {

            services.RegisterAllSimplePairings();
            services.RegisterOpenGenerics();
            services.SelfRegisterAllConcreteTypes();

            services.AddAutoMapper(expression =>
            {
                // TODO: probably get rid of this eventually as it's relatively imperformant
                expression.CreateMissingTypeMaps = true;
            });

            var appContext = services.AddMrCMSApps(context =>
            {
                context.RegisterApp<MrCMSCoreApp>();
                context.RegisterApp<MrCMSAdminApp>();
            });
            services.AddSingleton(appContext);

            var physicalProvider = Environment.ContentRootFileProvider;
            var compositeFileProvider =
                new CompositeFileProvider(new[] { physicalProvider }.Concat(appContext.ViewFileProviders));

            services.AddSingleton<IFileProvider>(compositeFileProvider);
            services.AddSession(options =>
            {
                //options.CookieHttpOnly = true;
            });

            services.AddMvc(options =>
                {
                    // add custom binder to beginning of collection
                    options.ModelBinderProviders.Insert(0, new SystemEntityBinderProvider());
                    options.ModelBinderProviders.Insert(1, new ExtendedModelBinderProvider());

                    // TODO: refactor this to be added per app
                    options.Filters.AddService(typeof(AdminAuthFilter));

                }).AddRazorOptions(options =>
                {
                    options.ViewLocationExpanders.Insert(0, new WebpageViewExpander());
                    options.ViewLocationExpanders.Insert(1, new AppViewLocationExpander());
                    options.FileProviders.Add(compositeFileProvider);
                })
                .AddViewLocalization()
                .AddDataAnnotations()
                .AddDataAnnotationsLocalization()
                .AddAppMvcConfig(appContext);


            services.AddSingleton<ISessionFactory>(provider => new NHibernateConfigurator(new SqlServer2012Provider(
                new DatabaseSettings
                {
                    ConnectionString =
                        "Data Source=localhost;Initial Catalog=mrcms-migration;Integrated Security=True;Persist Security Info=False;MultipleActiveResultSets=True"
                })).CreateSessionFactory());
            services.AddScoped<ISession>(provider =>
                provider.GetRequiredService<ISessionFactory>().OpenFilteredSession(provider));
            services.AddTransient<IStatelessSession>(provider =>
                provider.GetRequiredService<ISessionFactory>().OpenStatelessSession());

            services.AddIdentity<User, UserRole>(options =>
            {
                // lockout
                if (int.TryParse(Configuration["Auth:Lockout:MaxFailedAccessAttempts"],
                    out var maxFailedAccessAttempts))
                    options.Lockout.MaxFailedAccessAttempts = maxFailedAccessAttempts;
                if (TimeSpan.TryParse(Configuration["Auth:Lockout:DefaultLockoutTimeSpan"],
                    out var defaultLockoutTimeSpan))
                    options.Lockout.DefaultLockoutTimeSpan = defaultLockoutTimeSpan;
                if (bool.TryParse(Configuration["Auth:Lockout:AllowedForNewUsers"], out var allowedForNewUsers))
                    options.Lockout.AllowedForNewUsers = allowedForNewUsers;

                // password
                if (int.TryParse(Configuration["Auth:Password:RequiredLength"], out var requiredLength))
                    options.Password.RequiredLength = requiredLength;
                if (bool.TryParse(Configuration["Auth:Password:RequireDigit"], out var requireDigit))
                    options.Password.RequireDigit = requireDigit;
                if (bool.TryParse(Configuration["Auth:Password:RequireLowercase"], out var requireLowercase))
                    options.Password.RequireLowercase = requireLowercase;
                if (bool.TryParse(Configuration["Auth:Password:RequireNonAlphanumeric"],
                    out var requireNonAlphanumeric))
                    options.Password.RequireNonAlphanumeric = requireNonAlphanumeric;
                if (bool.TryParse(Configuration["Auth:Password:RequireUppercase"], out var requireUppercase))
                    options.Password.RequireUppercase = requireUppercase;

                // sign in
                if (bool.TryParse(Configuration["Auth:SignIn:RequireConfirmedEmail"],
                    out var requireConfirmedEmail))
                    options.SignIn.RequireConfirmedEmail = requireConfirmedEmail;
                if (bool.TryParse(Configuration["Auth:SignIn:RequireConfirmedPhoneNumber"],
                    out var requireConfirmedPhoneNumber))
                    options.SignIn.RequireConfirmedPhoneNumber = requireConfirmedPhoneNumber;

                options.User.RequireUniqueEmail = true;
                options.ClaimsIdentity.UserNameClaimType = nameof(User.Email);
                options.ClaimsIdentity.UserIdClaimType = nameof(User.Id);
            })
              .AddMrCMSStores()
              .AddDefaultTokenProviders();


            services.AddSingleton<ICmsMethodTester, CmsMethodTester>();
            services.AddSingleton<IAssignPageDataToRouteData, AssignPageDataToRouteData>();
            services.AddSingleton<IQuerySerializer, QuerySerializer>();
            services.AddSingleton<IStringLocalizerFactory, StringLocalizerFactory>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            //services.AddSingleton<IGetDefaultResourceValue, GetDefaultResourceValue>();
            //services.AddSingleton<ILocalizationManager, LocalizationManager>();


            services.AddScoped<IUrlHelper>(x => {
                var actionContext = x.GetRequiredService<IActionContextAccessor>().ActionContext;
                var factory = x.GetRequiredService<IUrlHelperFactory>();
                return factory.GetUrlHelper(actionContext);
            });
            services.AddScoped(provider => provider.GetService<IStringLocalizerFactory>()
                .Create(null, null));


            // TODO: update the resolution of this
            services.AddSingleton<IFileSystem, FileSystem>();

            services.ConfigureApplicationCookie(options =>
            {
                options.AccessDeniedPath
                    = new PathString("/login");
                options.LoginPath
                    = new PathString("/login");
                options.LogoutPath
                    = new PathString("/logout");
            });
            services.AddAuthentication();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, MrCMSAppContext appContext)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new CompositeFileProvider(
                    new[] { Environment.WebRootFileProvider }.Concat(appContext.ContentFileProviders))
            });
            app.UseSession();
            app.UseAuthentication();


            app.UseMvc(builder =>
            {
                builder.MapMrCMS();
                builder.MapMrCMSApps(appContext);

                builder.MapRoute(
                    "default",
                    "{controller=Home}/{action=Index}/{id?}");
            });

        }
    }
}
