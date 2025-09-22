using Microsoft.AspNetCore.Mvc;
using CommandsResult = ECK1.CommandsAPI.Commands;

namespace ECK1.CommandsAPI.Controllers;

public static class ControllerExtensions
{
    public static IActionResult ToResult(this ControllerBase controller, CommandsResult.ICommandResult result) => 
        result is CommandsResult.NotFound ? controller.NotFound() :
            result is CommandsResult.Success ?
                controller.Accepted(result) :
                controller.BadRequest(result);
}