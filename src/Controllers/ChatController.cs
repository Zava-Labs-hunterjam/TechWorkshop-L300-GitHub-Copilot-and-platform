using Microsoft.AspNetCore.Mvc;
using ZavaStorefront.Services;

namespace ZavaStorefront.Controllers;

public class ChatController : Controller
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromForm] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            ViewBag.Error = "Please enter a message.";
            return View("Index");
        }

        try
        {
            _logger.LogInformation("User sent chat message");
            var response = await _chatService.SendMessageAsync(message);
            ViewBag.UserMessage = message;
            ViewBag.AiResponse = response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AI response");
            ViewBag.UserMessage = message;
            ViewBag.Error = "Sorry, something went wrong while contacting the AI service. Please try again later.";
        }

        return View("Index");
    }
}
