using Maitre.Api.Common.Extensions;
using Maitre.Api.Features.Products;
using Maitre.Api.Features.Orders;
using Maitre.Api.Features.Reports;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapReportEndpoints();
app.Run();

