using Genisis.Api.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Genisis.Api.Tests;

/// <summary>
/// The Case / Worksheet handler (<see cref="ApiExceptionHandler"/>) and the membership handler (<c>ProblemDetailsExceptionHandler</c>) are both
/// registered, in that order. Each must answer its own exceptions and leave the other's alone.
/// </summary>
public class ExceptionHandlerCooperationTests
{
    private static async Task<(bool Handled, int Status)> Handle(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handled = await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance).TryHandleAsync(context, exception, default);
        return (handled, context.Response.StatusCode);
    }

    [Fact]
    public void The_case_handler_owns_its_business_rules_and_deadlocks_only()
    {
        Assert.True(ApiExceptionHandler.Owns(new BusinessRuleException("x")));
        Assert.True(ApiExceptionHandler.Owns(new NotFoundException("x")));
        Assert.True(ApiExceptionHandler.Owns(new ConflictException("x")));
        Assert.False(ApiExceptionHandler.Owns(new Genisis.Api.Common.Errors.BusinessRuleException("x")));
        Assert.False(ApiExceptionHandler.Owns(new InvalidOperationException("x")));
    }

    [Fact]
    public async Task It_answers_a_case_rule_and_passes_a_membership_rule_on()
    {
        Assert.Equal((true, StatusCodes.Status404NotFound), await Handle(new NotFoundException("Record not found!")));
        Assert.Equal((true, StatusCodes.Status409Conflict), await Handle(new ConflictException("clash")));
        Assert.Equal((true, StatusCodes.Status400BadRequest), await Handle(new BusinessRuleException("rule")));
        Assert.False((await Handle(new Genisis.Api.Common.Errors.BusinessRuleException("membership rule"))).Handled);
        Assert.False((await Handle(new InvalidOperationException("boom"))).Handled);
    }

    [Fact]
    public void Both_business_rule_families_share_one_base_class()
    {
        Assert.IsAssignableFrom<Genisis.Api.Common.Errors.BusinessRuleException>(new BusinessRuleException("x"));
        Assert.IsAssignableFrom<Genisis.Api.Common.Errors.BusinessRuleException>(new NotFoundException("x"));
    }
}
