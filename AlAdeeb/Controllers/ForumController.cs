using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using System;
using AlAdeeb.Data;
using AlAdeeb.Models;

namespace AlAdeeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController] 
    [Authorize]
    public class ForumController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ForumController(AppDbContext context) { _context = context; }

        public class PostDto { public int CourseId { get; set; } public string Content { get; set; } }
        public class ReplyDto { public int PostId { get; set; } public string Content { get; set; } }

        [HttpGet("{courseId}")]
        public async Task<IActionResult> GetCoursePosts(int courseId)
        {
            var posts = await _context.ForumPosts.Include(p => p.User).Include(p => p.Replies).ThenInclude(r => r.User).Where(p => p.CourseId == courseId).OrderByDescending(p => p.CreatedAt).Select(p => new
            {
                id = p.Id,
                content = p.Content,
                createdAt = p.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"),
                userName = p.User.FullName,
                userRole = p.User.Role,
                replies = p.Replies.OrderBy(r => r.CreatedAt).Select(r => new { id = r.Id, content = r.Content, createdAt = r.CreatedAt.ToString("yyyy/MM/dd hh:mm tt"), userName = r.User.FullName, userRole = r.User.Role })
            }).ToListAsync();
            return Ok(posts);
        }

        [HttpPost("post")]
        public async Task<IActionResult> CreatePost([FromBody] PostDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Content)) return BadRequest("Content is empty");
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            _context.ForumPosts.Add(new ForumPost { CourseId = model.CourseId, UserId = userId, Content = model.Content, CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("reply")]
        public async Task<IActionResult> CreateReply([FromBody] ReplyDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Content)) return BadRequest("Content is empty");
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            _context.ForumReplies.Add(new ForumReply { ForumPostId = model.PostId, UserId = userId, Content = model.Content, CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}