using Microsoft.AspNetCore.Mvc;

namespace Genisis.Api.Common;

/// <summary>Base of the API controllers: the logged-in user's code (the JWT name claim) for audit columns and logs.</summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>USR.USRCode of the caller.</summary>
    protected string UserCode => User.Identity?.Name ?? "";
}
