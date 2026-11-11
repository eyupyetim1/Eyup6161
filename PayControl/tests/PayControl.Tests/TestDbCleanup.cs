using Microsoft.Data.Sqlite;

namespace PayControl.Tests;

internal static class TestDbCleanup
{
    public static async Task DisposeProviderAndDbAsync(IAsyncDisposable provider, string dbPath)
    {
        await provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        for (var i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(dbPath))
                    File.Delete(dbPath);
                foreach (var side in new[] { dbPath + "-shm", dbPath + "-wal" })
                {
                    if (File.Exists(side))
                        File.Delete(side);
                }
                return;
            }
            catch (IOException) when (i < 4)
            {
                await Task.Delay(50 * (i + 1));
            }
            catch (IOException)
            {
                // Windows CI: kilitli dosya kalabilir; test sonucunu etkilemesin.
                return;
            }
        }
    }
}
