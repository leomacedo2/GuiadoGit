using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.DTOs;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers;

[ApiController]
[Route("api/github")]
public sealed class GitHubController(IGitHubService gitHubService) : ControllerBase
{
    [HttpGet("profile/{username}")]
    [ProducesResponseType<PortfolioDto>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(429)]
    [ProducesResponseType<ProblemDetails>(502)]
    [ProducesResponseType<ProblemDetails>(504)]
    public async Task<ActionResult<PortfolioDto>> GetProfile(string username, CancellationToken cancellationToken)
    {
        username = username.Trim();
        if (!Regex.IsMatch(username, @"\A[a-zA-Z0-9](?:[a-zA-Z0-9]|-(?=[a-zA-Z0-9])){0,38}\z"))
            return Problem(statusCode: 400, detail: "Informe um username válido do GitHub, com até 39 caracteres, sem @ ou URL.");
        try
        {
            return Ok(await gitHubService.GetPortfolioAsync(username, cancellationToken));
        }
        catch (GitHubApiException exception)
        {
            return Problem(statusCode: exception.StatusCode, detail: exception.Message);
        }
    }
}
