using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/members")]
public class MembersController : ControllerBase
{
    private readonly MongoDbService _mongo;
    public MembersController(MongoDbService mongo) => _mongo = mongo;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetAll()
    {
        try
        {
            var col = _mongo.GetCollection<Member>("members");
            var list = await col.Find(x => !x.IsDeleted && x.IsActive).SortBy(x => x.Order).ToListAsync();
            return Ok(list.Select(Map));
        }
        catch { return StatusCode(503); }
    }

    [HttpGet("admin")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetAllAdmin()
    {
        var col = _mongo.GetCollection<Member>("members");
        var list = await col.Find(x => !x.IsDeleted).SortBy(x => x.Order).ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<MemberDto>> GetById(string id)
    {
        var col = _mongo.GetCollection<Member>("members");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        return Ok(Map(m));
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<MemberDto>> Create([FromBody] CreateMemberDto dto)
    {
        var col = _mongo.GetCollection<Member>("members");
        var user = User.Identity?.Name ?? "system";
        var m = new Member
        {
            Name = dto.Name, Role = dto.Role, ImageUrl = dto.ImageUrl, ImageAlt = dto.ImageAlt,
            ProfileUrl = dto.ProfileUrl, Order = dto.Order, IsActive = dto.IsActive,
            CreatedBy = user, UpdatedBy = user
        };
        try{ await col.InsertOneAsync(m); } catch(Exception ex){ return StatusCode(503, new { detail = "Mongo no disponible (whitelist/IP). "+ex.Message }); }
        return CreatedAtAction(nameof(GetById), new { id = m.Id }, Map(m));
    }

    [HttpPut("{id}")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<MemberDto>> Update(string id, [FromBody] UpdateMemberDto dto)
    {
        var col = _mongo.GetCollection<Member>("members");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        m.Name = dto.Name; m.Role = dto.Role; m.ImageUrl = dto.ImageUrl; m.ImageAlt = dto.ImageAlt;
        m.ProfileUrl = dto.ProfileUrl; m.Order = dto.Order; m.IsActive = dto.IsActive;
        m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, m);
        return Ok(Map(m));
    }

    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<IActionResult> Delete(string id)
    {
        var col = _mongo.GetCollection<Member>("members");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, m);
        return NoContent();
    }

    private static MemberDto Map(Member m) => new(m.Id, m.Name, m.Role, m.ImageUrl, m.ImageAlt, m.ProfileUrl, m.Order, m.IsActive, m.CreatedAt, m.CreatedBy, m.UpdatedAt, m.UpdatedBy);
}
