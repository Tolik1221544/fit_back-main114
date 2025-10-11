using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FitnessTracker.API.Data;
using Microsoft.Extensions.Hosting;

namespace FitnessTracker.API.Tests.IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // ������������� �������� ���������� ����
            builder.UseContentRoot(Directory.GetCurrentDirectory());

            builder.ConfigureServices(services =>
            {
                // ������� �������� ��
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // ��������� InMemory �� ��� ������
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase_" + Guid.NewGuid());
                });

                // ������ ����� ��
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();
            });

            builder.UseEnvironment("Testing");
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // ��������� ContentRoot ��� ������
            var projectDir = Directory.GetCurrentDirectory();
            builder.UseContentRoot(projectDir);

            return base.CreateHost(builder);
        }
    }
}