using System;
using System.Threading.Tasks;

namespace Nexorsys.NFC.APP.Services;

public interface IKioskApplicationSessionClient
{
    Task<Guid?> StartApplicationSessionAsync(string applicationId);
    Task StopApplicationSessionAsync(Guid applicationSessionId);
}
