using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared.Abstractions.Core.Domain.Events;
using Shared.Abstractions.Ef;
using Shared.EF;
using Shared.EF.Extensions;
using Vertical.Slice.Template.Shared.Data;
using Vertical.Slice.Template.Shared.Workers;

namespace Vertical.Slice.Template.Shared.Extensions.WebApplicationBuilderExtensions;

public static partial class WebApplicationBuilderExtensions
{
    public static void AddStorage(this WebApplicationBuilder builder)
    {
        if (builder.Configuration.GetValue<bool>($"{nameof(PostgresOptions)}:{nameof(PostgresOptions.UseInMemory)}"))
        {
            builder.Services.AddDbContext<CatalogsDbContext>(options => options.UseInMemoryDatabase("Catalogs"));

            builder.Services.AddScoped<IDbFacadeResolver>(provider => provider.GetService<CatalogsDbContext>()!);
            builder.Services.AddScoped<IDomainEventContext>(provider => provider.GetService<CatalogsDbContext>()!);
        }
        else
        {
            builder.Services.AddPostgresDbContext<CatalogsDbContext>();

            builder.Services.AddHostedService<MigrationWorker>();
            builder.Services.AddHostedService<SeedWorker>();
        }
    }
}