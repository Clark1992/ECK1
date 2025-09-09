using Microsoft.AspNetCore.Mvc;

namespace ECK1.QueriesAPI.Controllers;

public static class ControllerExtensions
{
    public static IActionResult ToResult(this ControllerBase controller, object result) => 
        result is null ? controller.NotFound() : controller.Ok(result);
}