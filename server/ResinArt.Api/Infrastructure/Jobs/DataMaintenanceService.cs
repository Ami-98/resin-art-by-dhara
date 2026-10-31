using System.IO.Compression;
using Microsoft.Extensions.Options;
using ResinArt.Api.Features.Leads;
using ResinArt.Api.Infrastructure.Settings;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Infrastructure.Jobs;

public sealed record BackupArchiveInfo(string FileName, long SizeBytes, DateTimeOffset CreatedAt);

public sealed record MaintenanceReport(
    BackupArchiveInfo? BackupCreated,
    int ExpiredContactsDeleted,
    int ExpiredRequestsDeleted,
    int OrphanedTempFilesDeleted,
    int OldBackupsPruned);

/// <summary>
/// Creates ZIP snapshots of <c>{dataRoot}/db</c> and <c>{dataRoot}/config</c> in <c>{dataRoot}/backups/</c>,
/// prunes old archives, deletes orphaned temp uploads, and enforces the privacy retention
/// window on leads. Also runs automatically on the configured interval via <see cref="DataMaintenanceHostedService"/>.
/// </summary>
public sealed class DataMaintenanceService(
    DataRoot dataRoot,
    IDocumentStore<ContactDocument> contacts,
    IDocumentStore<CustomRequestDocument> customRequests,
    IOptionsMonitor<MaintenanceOptions> options,
    ILogger<DataMaintenanceService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public IReadOnlyList<BackupArchiveInfo> ListBackups()
    {
        if (!Directory.Exists(dataRoot.BackupsPath)) return [];

        return new DirectoryInfo(dataRoot.BackupsPath)
            .GetFiles("snapshot-*.zip")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupArchiveInfo(f.Name, f.Length, f.CreationTimeUtc))
            .ToList();
    }

    public string? ResolveBackupFilePath(string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (!safeName.StartsWith("snapshot-", StringComparison.Ordinal) ||
            !safeName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var path = Path.Combine(dataRoot.BackupsPath, safeName);
        return File.Exists(path) ? path : null;
    }

    public async Task<MaintenanceReport> RunFullMaintenanceAsync(
        string reason = "manual",
        int? keepBackups = null,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var opts = options.CurrentValue;
            var retainCount = keepBackups ?? opts.KeepBackupsCount;
            var maxTempAge = TimeSpan.FromHours(Math.Max(1, opts.OrphanedTempFileMaxAgeHours));

            var backup = CreateSnapshotZip(reason);
            var pruned = PruneOldBackups(retainCount);
            var tempDeleted = CleanOrphanedTempUploads(maxTempAge);
            var (expiredContacts, expiredRequests) = await EnforceLeadRetentionAsync(ct);

            logger.LogInformation(
                "Maintenance ({Reason}) complete: Backup={Backup}, ExpiredContacts={Contacts}, ExpiredRequests={Requests}, TempFiles={Temp}, PrunedBackups={Pruned}",
                reason,
                backup.FileName,
                expiredContacts,
                expiredRequests,
                tempDeleted,
                pruned);

            return new MaintenanceReport(
                backup,
                expiredContacts,
                expiredRequests,
                tempDeleted,
                pruned);
        }
        finally
        {
            _gate.Release();
        }
    }

    private BackupArchiveInfo CreateSnapshotZip(string reason)
    {
        Directory.CreateDirectory(dataRoot.BackupsPath);
        var safeReason = new string(reason.Where(char.IsLetterOrDigit).Take(16).ToArray());
        if (string.IsNullOrWhiteSpace(safeReason)) safeReason = "auto";

        var fileName = $"snapshot-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{safeReason}.zip";
        var fullPath = Path.Combine(dataRoot.BackupsPath, fileName);

        using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            AddFolderToZip(archive, dataRoot.DbPath, "db");
            AddFolderToZip(archive, dataRoot.ConfigPath, "config");
        }

        var info = new FileInfo(fullPath);
        return new BackupArchiveInfo(info.Name, info.Length, DateTimeOffset.UtcNow);
    }

    private static void AddFolderToZip(ZipArchive archive, string folderPath, string prefix)
    {
        if (!Directory.Exists(folderPath)) return;
        foreach (var file in Directory.GetFiles(folderPath, "*.json"))
        {
            var entryName = $"{prefix}/{Path.GetFileName(file)}";
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        }
    }

    private int PruneOldBackups(int keepCount)
    {
        if (!Directory.Exists(dataRoot.BackupsPath)) return 0;
        var files = new DirectoryInfo(dataRoot.BackupsPath)
            .GetFiles("snapshot-*.zip")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(Math.Max(1, keepCount))
            .ToList();

        foreach (var f in files)
        {
            try { f.Delete(); } catch { /* ignore */ }
        }
        return files.Count;
    }

    private int CleanOrphanedTempUploads(TimeSpan maxAge)
    {
        var tempDir = Path.Combine(dataRoot.UploadsPath, "temp");
        if (!Directory.Exists(tempDir)) return 0;

        var cutoff = DateTime.UtcNow - maxAge;
        var deleted = 0;
        foreach (var f in new DirectoryInfo(tempDir).GetFiles())
        {
            if (f.CreationTimeUtc < cutoff)
            {
                try
                {
                    f.Delete();
                    deleted++;
                }
                catch { /* ignore */ }
            }
        }
        return deleted;
    }

    private async Task<(int ContactsDeleted, int RequestsDeleted)> EnforceLeadRetentionAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredContacts = await contacts.QueryAsync(c => c.RetainUntil != default && c.RetainUntil <= now, ct);
        foreach (var c in expiredContacts)
            await contacts.DeleteAsync(c.Id, ct);

        var expiredRequests = await customRequests.QueryAsync(r => r.RetainUntil != default && r.RetainUntil <= now, ct);
        foreach (var r in expiredRequests)
        {
            await customRequests.DeleteAsync(r.Id, ct);
            var reqUploadDir = Path.Combine(dataRoot.UploadsPath, "requests", r.Id);
            if (Directory.Exists(reqUploadDir))
            {
                try { Directory.Delete(reqUploadDir, recursive: true); } catch { /* ignore */ }
            }
        }

        return (expiredContacts.Count, expiredRequests.Count);
    }
}

public sealed class DataMaintenanceHostedService(
    DataMaintenanceService maintenance,
    IOptionsMonitor<MaintenanceOptions> options,
    ILogger<DataMaintenanceHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = Math.Max(1, options.CurrentValue.IntervalHours);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await maintenance.RunFullMaintenanceAsync("daily", ct: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Scheduled daily maintenance failed.");
            }
        }
    }
}
