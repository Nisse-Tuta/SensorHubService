
using Microsoft.Extensions.Options;
using RaspSensorService;
using System.Net;
using System.Security.Cryptography.X509Certificates;
namespace SensorHubService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            if (OperatingSystem.IsLinux()) { 
                var certPem = File.ReadAllText("/etc/letsencrypt/live/hallon.hopto.org/fullchain.pem");
                var keyPem = File.ReadAllText("/etc/letsencrypt/live/hallon.hopto.org/privkey.pem");
                var x509 = X509Certificate2.CreateFromPem(certPem, keyPem);
                builder.WebHost.ConfigureKestrel((serverOptions) =>
                {
                    serverOptions.ConfigureHttpsDefaults(adapterOptions =>
                    {
                        adapterOptions.ServerCertificate = X509Certificate2.CreateFromPem(certPem, keyPem);
                    });
                });
            }


            Console.WriteLine($"Application Name: {builder.Environment.ApplicationName}");
            Console.WriteLine($"Environment Name: {builder.Environment.EnvironmentName}");
            Console.WriteLine($"ContentRoot Path: {builder.Environment.ContentRootPath}");
            Console.WriteLine($"WebRootPath: {builder.Environment.WebRootPath}");

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            //builder.Services.AddEndpointsApiExplorer();
            //builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();
            builder.Services.AddHostedService<Worker>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<RaspSensorHub>("/raspberrySensors");

            app.Run();
        }
    }
}
