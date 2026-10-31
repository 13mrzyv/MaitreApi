using Maitre.Api.Features.Reports.GetTopSellingProducts;

namespace Maitre.Api.Features.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/restaurants/{restaurantId}/reports")
            .WithTags("Reports");
        group.MapGetTopSellingProducts();
        return endpoints;
    }
}
