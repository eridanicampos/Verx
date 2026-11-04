using CashFlow.Transactions.Domain.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Transactions.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult Problem(Error error)
    {
        var result = Problem(title: error.Code, detail: error.Message, statusCode: StatusCodeOf(error.Type));
        if (result.Value is ProblemDetails problem)
        {
            problem.Extensions["code"] = error.Code;
        }

        return result;
    }

    protected ObjectResult Problem(string code, string detail, int statusCode)
    {
        var result = Problem(title: code, detail: detail, statusCode: statusCode);
        if (result.Value is ProblemDetails problem)
        {
            problem.Extensions["code"] = code;
        }

        return result;
    }

    private static int StatusCodeOf(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };
}
