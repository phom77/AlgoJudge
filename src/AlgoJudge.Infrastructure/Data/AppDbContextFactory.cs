using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace AlgoJudge.Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var apiDirectoryCandidates = new[]
            {
                Path.Combine(currentDirectory, "src", "AlgoJudge.API"),
                Path.Combine(currentDirectory, "..", "AlgoJudge.API")
            };

            var basePath = apiDirectoryCandidates.FirstOrDefault(Directory.Exists)
                ?? throw new DirectoryNotFoundException(
                    "Could not locate src/AlgoJudge.API for design-time configuration.");

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json")
                .Build();

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            builder.UseNpgsql(connectionString);

            return new AppDbContext(builder.Options);
        }
    }
}
