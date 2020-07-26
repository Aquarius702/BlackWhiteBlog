using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using BlackWhiteBlog.DbModels;
using BlackWhiteBlog.Helpers.Paging;
using BlackWhiteBlog.TransferObjects.Post;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlackWhiteBlog.Controllers.Posts
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        private readonly BlogDbContext _ctx;
        public PostController(BlogDbContext ctx)
        {
            _ctx = ctx;
        }
        // GET: api/Posts
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] GetPostsDto filter)
        {
            var result = new List<PostCardDto>();
            
            var posts = await _ctx.Posts.OrderByDescending(x => x.PostDate)
                .Include(x => x.PostContents
                    .Where(pc => pc.PostColor == filter.PostColor))
                .GetPaged(filter.CurrentPage, filter.PageSize);
            
            foreach (var post in posts.Results)
            {
                var postContent = post.PostContents.FirstOrDefault();
                if (postContent == null)
                    continue;
                
                var content = await GetTextFromHtml(postContent.Content);
                var dto = new PostCardDto()
                {
                    Title = postContent.Title,
                    PicLink = postContent.ImageLink,
                    PostDate = post.PostDate,
                    PostId = post.PostId,
                    TextContent = content
                };
                result.Add(dto);
            }

            if (result.Any())
            {
                return Ok(result);
            }
            else return NotFound();
        }

        private async Task<string> GetTextFromHtml(string html)
        {
            //Use the default configuration for AngleSharp
            var config = Configuration.Default;

            //Create a new context for evaluating webpages with the given config
            var context = BrowsingContext.New(config);

            //Just get the DOM representation
            var document = await context.OpenAsync(req => req.Content(html));
            return document.TextContent;
        }
        
        // GET: api/Posts/5/1
        [HttpGet("{id}/{color}", Name = "Get")]
        public async Task<IActionResult> Get(int id, int color)
        {
            var post = await _ctx.Posts
                .Include(x => x.Author)
                .Include(x => x.PostContents
                    .Where(pc => pc.PostColor == color))
                .FirstOrDefaultAsync(x => x.PostId == id);
            if (post == null)
                return NotFound();
            
            var postContent = post.PostContents?.FirstOrDefault();
            var result = new FullPostDto()
            {
                PostId = post.PostId,
                AuthorId = post.AuthorId,
                AuthorName = post.Author?.AuthorName,
                Title = postContent?.Title,
                PicLink = postContent?.ImageLink,
                PostDate = post.PostDate,
                HtmlContent = postContent?.Content
            };
            
            if (!string.IsNullOrEmpty(result.HtmlContent))
                return Ok(result);
            else return NotFound();
        }
        
        // POST: api/Posts
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreatePostDto value)
        {
            //todo: проверка прав юзера
            
            var author = await _ctx.Authors
                .FirstOrDefaultAsync(x => x.AuthorId == value.AuthorId);
            if (author == null)
                return BadRequest("Ошибка: автор не зарегистрирован");
            
            //создание поста
            var post = new Post()
            {
                Author = author,
                PostDate = DateTime.UtcNow
            };
            //создание контентов к нему
            var postContents = value.Contents.Select(content => new PostContent()
                {
                    Title = content.Title,
                    ImageLink = content.PicLink,
                    Content = content.HtmlContent,
                    PostColor = content.Color,
                    Post = post
                })
                .ToList();
            //соединяем пост с контентами
            post.PostContents = postContents;
            //добавляем созданный пост
            await _ctx.AddAsync(post);
            //сохраняем изменения
            var result = await _ctx.SaveChangesAsync();
            
            if (result > 0)
                return Ok(post.PostId);
            return BadRequest("Не удалось создать пост");
        }

        // PUT: api/Posts/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody]PostContentDto value)
        {
            //todo: проверка прав юзера
            
            //находим контент поста с таким цветом
            var postContent = await _ctx.PostContents
                .FirstOrDefaultAsync(pc => pc.PostId == id && pc.PostColor == value.Color);
            if (postContent == null)
                return NotFound("Пост не найден");
            
            //обновляем контент
            postContent.Content = value.HtmlContent;
            postContent.Title = value.Title;
            postContent.ImageLink = value.PicLink;
            
            //сохраняем результат
            var result = await _ctx.SaveChangesAsync();
            
            if (result > 0)
                return Ok();
            return NotFound("Не удалось обновить пост");
        }
    }
}