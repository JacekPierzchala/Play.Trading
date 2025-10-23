using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Play.Common.MassTransit;
using Play.Common.MongoDb;
using Play.Common.Settings;
using Play.Trading.Service.StateMachines;
using ZstdSharp.Unsafe;

namespace Play.Trading.Service
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddMongo()
            .AddJwtBearerAuthentication();

            AddMassTransit(services);

            services.AddControllers(opt =>
            {
                opt.SuppressAsyncSuffixInActionNames = false;
            }).AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition
                    = JsonIgnoreCondition.WhenWritingNull;
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Trading.Service", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Trading.Service v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void AddMassTransit(IServiceCollection services)
        {
            services.AddMassTransit(conf =>
            {
                conf.UsingPlayEconomyRabbitMq();
                conf.AddConsumers(Assembly.GetEntryAssembly());
                conf.AddSagaStateMachine<PurchaseStateMachine, PurchaseState>()
                .MongoDbRepository(r =>
                {
                    var serviceSettings = Configuration.GetSection(nameof(ServiceSettings))
                                        .Get<ServiceSettings>();

                    var mongoDbSettings = Configuration.GetSection(nameof(MongoDbSettings))
                                       .Get<MongoDbSettings>();
                    r.Connection = mongoDbSettings.ConnectionString;
                    r.DatabaseName = serviceSettings.ServiceName;

                });

            });

            services.AddMassTransitHostedService();
            services.AddGenericRequestClient();
        }
    }
}
