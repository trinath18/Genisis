using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Genisis.Api.Common;

/// <summary>
/// Base class for request validators. The first failing rule is the one reported (the desktop forms showed one message at a
/// time, in a fixed order), so rules are declared in the order the user should fix them.
/// </summary>
public abstract class RequestValidator<T> : AbstractValidator<T>
{
    /// <summary>Stops at the first failing rule.</summary>
    protected RequestValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;
    }
}

/// <summary>
/// Runs the registered <see cref="IValidator{T}"/> of every action argument before the action executes and answers
/// HTTP 400 <c>{ "message": first error }</c> when one fails. Controllers therefore never validate by hand.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            if (services.GetService(typeof(IValidator<>).MakeGenericType(argument.GetType())) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            context.Result = new BadRequestObjectResult(new { message = result.Errors[0].ErrorMessage });
            return;
        }

        await next();
    }
}
