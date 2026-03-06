using ECK1.CommandsAPI.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ECK1.CommandsAPI.Data;

internal static class OptimisticEventStoreSaveHelper
{
    public static void ThrowIfUnexpectedVersion(
        IAggregateRoot aggregate,
        int currentVersion)
    {
        if (currentVersion != aggregate.Version)
        {
            throw new ConcurrencyConflictException(aggregate, currentVersion);
        }
    }

    public static ConcurrencyConflictException CreateConflictException(
        IAggregateRoot aggregate,
        int currentVersion,
        string phase) =>
        new (aggregate, currentVersion, phase);

    public static bool IsDuplicateVersionConflict(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlEx)
        {
            return false;
        }

        return sqlEx.Number is 2601 or 2627;
    }
}
