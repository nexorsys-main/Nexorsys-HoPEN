using System.Collections.Concurrent;

namespace Nexorsys.Identity.API.Services
{
	public class ActiveKioskService
	{
		private readonly ConcurrentDictionary<(Guid OrganizationId, Guid WorkstationId), (string MachineName, DateTime LastSeen, bool IsReaderConnected)> _activeKiosks = new();

		public void RegisterHeartbeat(Guid organizationId, Guid workstationId, string machineName, bool isReaderConnected = false)
		{
			if (organizationId != Guid.Empty && workstationId != Guid.Empty && !string.IsNullOrWhiteSpace(machineName))
			{
				_activeKiosks[(organizationId, workstationId)] = (machineName, DateTime.UtcNow, isReaderConnected);
			}
		}

		public IEnumerable<KioskStatus> GetActiveKiosks(Guid organizationId)
		{
			if (organizationId == Guid.Empty) return [];
			var cutoff = DateTime.UtcNow.AddSeconds(-90); // Clinical network stability margin

			return _activeKiosks
				.Where(k => k.Key.OrganizationId == organizationId)
				.Select(k => new KioskStatus
				{
					MachineName = k.Value.MachineName,
					LastSeen = k.Value.LastSeen,
					IsOnline = k.Value.LastSeen >= cutoff,
					IsReaderConnected = k.Value.IsReaderConnected
				})
				.OrderByDescending(k => k.IsOnline)
				.ThenByDescending(k => k.LastSeen);
		}
	}

	public class KioskStatus
	{
		public string MachineName { get; set; } = string.Empty;
		public DateTime LastSeen { get; set; }
		public bool IsOnline { get; set; }
		public bool IsReaderConnected { get; set; }
	}
}
