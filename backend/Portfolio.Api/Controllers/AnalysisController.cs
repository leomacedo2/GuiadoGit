using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.DTOs;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public sealed class AnalysisController(PersistentAnalysisService analysis) : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<ActionResult<AnalysisDto>> Get(string username, CancellationToken cancellationToken)
        => await Analyze(username, false, cancellationToken);

    [HttpPost("{username}/refresh")]
    public async Task<ActionResult<AnalysisDto>> Refresh(string username, CancellationToken cancellationToken)
        => await Analyze(username, true, cancellationToken);

    private async Task<ActionResult<AnalysisDto>> Analyze(string username, bool force, CancellationToken cancellationToken)
    {
        username = username.Trim();
        if (!Regex.IsMatch(username, @"\A[a-zA-Z0-9](?:[a-zA-Z0-9]|-(?=[a-zA-Z0-9])){0,38}\z"))
            return Problem(statusCode: 400, detail: "Informe um username válido do GitHub, sem @ ou URL.");
        try
        {
            var result = await analysis.GetAsync(username, force, cancellationToken);
            return Ok(result);
        }
        catch (GitHubApiException ex) { return Problem(statusCode: ex.StatusCode, detail: ex.Message); }
    }
}
