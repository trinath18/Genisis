using Genisis.Api.Common;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace Genisis.Api.Tests;

/// <summary>The Swagger document of every endpoint must generate (a failure here is the 500 on /swagger/v1/swagger.json).</summary>
public class SwaggerDocumentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void The_whole_document_generates()
    {
        var document = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.NotEmpty(document.Paths);
        Assert.Contains("Mma.MmaProcedureRequest", document.Components.Schemas.Keys);
        Assert.Contains("Case.Mma.MmaProcedureRequest", document.Components.Schemas.Keys);
    }

    [Fact]
    public void Schema_ids_carry_the_namespace_and_generic_arguments()
    {
        Assert.Equal("Case.Mma.MmaProcedureRequest", SwaggerSchemaIds.For(typeof(Case.Mma.MmaProcedureRequest)));
        Assert.Equal("ListOfMaintenance.Security.UserSummary", SwaggerSchemaIds.For(typeof(List<Maintenance.Security.UserSummary>)));
        Assert.Equal("String", SwaggerSchemaIds.For(typeof(string)));
    }
}
