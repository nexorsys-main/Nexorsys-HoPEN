using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Nexorsys.Identity.API.Controllers
{
	[ApiController]
	[Route("api/federation")]
	[Authorize]
	public class FederationController : ControllerBase
	{
		[HttpGet("providers")]
		public IActionResult GetActiveProviders()
		{
			return Ok(new
			{
				available = false,
				message = "Federation provider discovery is not configured for this organization.",
				providers = Array.Empty<object>()
			});
		}

		[HttpPost("callback/psi")]
		public async Task<IActionResult> PsiCallback([FromBody] object payload)
		{
			return StatusCode(501, new { message = "Pro Santé Connect callback validation is not configured." });
		}

		[HttpPost("callback/ecps")]
		public async Task<IActionResult> EcpsCallback([FromBody] object payload)
		{
			return StatusCode(501, new { message = "e-CPS callback validation is not configured." });
		}
	}
}
