using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Nexorsys.Identity.API.Hubs;
using System.Security.Cryptography;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize(Policy = "AdminOnly")]
	public class NfcManagementController : ControllerBase
	{
		public static System.Collections.Concurrent.ConcurrentDictionary<string, string> PendingWrites = new();

		[HttpPost("write-cuid")]
		public IActionResult TriggerWriteCuid([FromBody] WriteCuidRequest request)
		{
			var newCuid = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
			if (!string.IsNullOrWhiteSpace(request.TargetMachine))
			{
				PendingWrites[request.TargetMachine] = newCuid;
			}
			return Ok(new { success = true, newCuid = newCuid, message = "Command sent to write CUID." });
		}
	}

	public class WriteCuidRequest
	{
		public string TargetMachine { get; set; } = string.Empty;
	}
}
