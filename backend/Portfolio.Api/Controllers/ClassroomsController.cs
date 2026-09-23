using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Portfolio.Api.Data;
using Portfolio.Api.DTOs;
using Portfolio.Api.Services;

namespace Portfolio.Api.Controllers;

[ApiController, Authorize, Route("api/classes"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ClassroomsController(PortfolioDbContext db, ClassroomDashboardService dashboards, TimeProvider clock) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private const int MaxMembers = 200;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await dashboards.ListAsync(UserId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await dashboards.GetAsync(id, UserId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost, RequestSizeLimit(32768)]
    public async Task<IActionResult> Create(CreateClassroomRequest request, CancellationToken ct)
    {
        if (!ValidName(request.Name)) return InvalidName();
        var ids = request.ProfileIds.Distinct().ToArray();
        if (!await AreSaved(ids, ct)) return Unsaved();
        var now = clock.GetUtcNow();
        var classroom = new Classroom { ApplicationUserId = UserId, Name = request.Name.Trim(), CreatedAt = now, UpdatedAt = now };
        classroom.Members = ids.Select(id => new ClassroomMember { ClassroomId = classroom.Id, ApplicationUserId = UserId, GitHubProfileId = id, AddedAt = now }).ToList();
        db.Classrooms.Add(classroom);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        { return Problem(statusCode: 409, detail: "A lista de perfis salvos mudou. Recarregue os alunos e tente novamente."); }
        return CreatedAtAction(nameof(Get), new { id = classroom.Id }, new { classroom.Id });
    }

    [HttpPut("{id:guid}"), RequestSizeLimit(32768)]
    public Task<IActionResult> Rename(Guid id, RenameClassroomRequest request, CancellationToken ct) => Change(id, classroom =>
    {
        if (!ValidName(request.Name)) return Task.FromResult(InvalidName());
        classroom.Name = request.Name.Trim();
        return Task.FromResult<IActionResult>(NoContent());
    }, ct);

    [HttpPost("{id:guid}/members"), RequestSizeLimit(32768)]
    public Task<IActionResult> AddMembers(Guid id, AddClassroomMembersRequest request, CancellationToken ct) => Change(id, async classroom =>
    {
        var ids = request.ProfileIds.Distinct().ToArray();
        if (!await AreSaved(ids, ct)) return Unsaved();
        var existing = await db.ClassroomMembers.Where(m => m.ClassroomId == id).Select(m => m.GitHubProfileId).ToListAsync(ct);
        if (ids.Any(existing.Contains)) return Problem(statusCode: 409, detail: "Um dos perfis selecionados já pertence a esta turma. Recarregue a lista.");
        if (existing.Count + ids.Length > MaxMembers) return Problem(statusCode: 400, detail: "Uma turma pode ter até 200 alunos nesta versão.");
        db.ClassroomMembers.AddRange(ids.Select(profileId => new ClassroomMember { ClassroomId = id, ApplicationUserId = UserId, GitHubProfileId = profileId, AddedAt = clock.GetUtcNow() }));
        return NoContent();
    }, ct);

    [HttpDelete("{id:guid}/members/{profileId:guid}")]
    public Task<IActionResult> RemoveMember(Guid id, Guid profileId, CancellationToken ct) => Change(id, async classroom =>
    {
        var member = await db.ClassroomMembers.SingleOrDefaultAsync(m => m.ClassroomId == id && m.GitHubProfileId == profileId, ct);
        if (member is null) return NotFound();
        if (await db.ClassroomMembers.CountAsync(m => m.ClassroomId == id, ct) <= 1)
            return Problem(statusCode: 409, detail: "A turma precisa manter pelo menos um aluno. Adicione outro aluno ou exclua a turma.");
        db.ClassroomMembers.Remove(member);
        return NoContent();
    }, ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) => Change(id, classroom =>
    {
        db.Classrooms.Remove(classroom);
        return Task.FromResult<IActionResult>(NoContent());
    }, ct);

    private async Task<IActionResult> Change(Guid id, Func<Classroom, Task<IActionResult>> change, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize membership edits per classroom in PostgreSQL, including concurrent last-member removals.
        var classroom = await db.Classrooms.FromSqlInterpolated($"SELECT * FROM portfolio.\"Classrooms\" WHERE \"Id\" = {id} AND \"ApplicationUserId\" = {UserId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (classroom is null) return NotFound();
        var result = await change(classroom);
        if (result is not NoContentResult) return result;
        classroom.UpdatedAt = clock.GetUtcNow();
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.UniqueViolation })
        { return Problem(statusCode: 409, detail: "Os vínculos da turma mudaram durante a operação. Recarregue a página e tente novamente."); }
    }

    private async Task<bool> AreSaved(Guid[] ids, CancellationToken ct) => ids.Length is >= 1 and <= MaxMembers &&
        await db.UserSavedProfiles.CountAsync(s => s.UserId == UserId && ids.Contains(s.GitHubProfileId), ct) == ids.Length;
    private static bool ValidName(string name) => name.Trim().Length is >= 2 and <= 100 && !name.Any(char.IsControl);
    private IActionResult InvalidName() => Problem(statusCode: 400, detail: "Informe um nome de turma com 2 a 100 caracteres, sem caracteres de controle.");
    private IActionResult Unsaved() => Problem(statusCode: 400, detail: "Selecione de 1 a 200 perfis que estejam em Minhas análises da sua conta.");
}
