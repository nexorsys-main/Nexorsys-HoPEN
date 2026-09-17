using Npgsql;

namespace Nexorsys.Identity.API.Services;

public static class DatabaseConcurrencyErrors
{
    public static bool IsSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: "40001" }) return true;
        }
        return false;
    }
}
