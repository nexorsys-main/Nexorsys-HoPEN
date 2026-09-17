namespace Nexorsys.Identity.Core
{
	using System;

	public class IdentifyRequest
	{
		public string NfcUid { get; set; } = string.Empty;
	}

	public class IdentifyResponse
	{
		public Guid SessionId { get; set; }
		public bool IsIdentified { get; set; }
		public User? User { get; set; }
	}

	public class ValidatePinRequest
	{
		public Guid SessionId { get; set; }
		public string Pin { get; set; } = string.Empty;
	}

	public class ValidatePinResponse
	{
		public bool IsValidated { get; set; }
	}

	public class StartSessionRequest
	{
		public Guid SessionId { get; set; }
	}

	public class EndSessionRequest
	{
		public Guid SessionId { get; set; }
	}

	public class LoginRequest
	{
		public string Username { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
	}

	public class LoginResponse
	{
		public string Token { get; set; } = string.Empty;
		public User User { get; set; } = null!;
	}

	public class ForgotPasswordRequest
	{
		public string Email { get; set; } = string.Empty;
	}

	public class ResetPasswordRequest
	{
		public string Token { get; set; } = string.Empty;
		public string NewPassword { get; set; } = string.Empty;
	}
}
