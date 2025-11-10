using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using KosovaPOS.Models;
using KosovaPOS.Database;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Enterprise-grade audit logging service
    /// Tracks all system operations for compliance and security
    /// </summary>
    public class AuditService
    {
        private readonly POSDbContext _context;
        private static string? _currentUserId;
        private static string? _currentUserName;

        public AuditService(POSDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public static void SetCurrentUser(string userId, string userName)
        {
            _currentUserId = userId;
            _currentUserName = userName;
        }

        public async Task LogAsync(
            string action,
            string entityType,
            int? entityId = null,
            string? entityName = null,
            object? oldValue = null,
            object? newValue = null,
            string? details = null,
            bool isSuccess = true,
            string? errorMessage = null)
        {
            try
            {
                var auditLog = new AuditLog
                {
                    Timestamp = DateTime.Now,
                    UserId = _currentUserId ?? "SYSTEM",
                    UserName = _currentUserName ?? "System",
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    EntityName = entityName,
                    OldValue = oldValue != null ? JsonSerializer.Serialize(oldValue) : null,
                    NewValue = newValue != null ? JsonSerializer.Serialize(newValue) : null,
                    Details = details,
                    IsSuccess = isSuccess,
                    ErrorMessage = errorMessage
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log to console if database logging fails
                Console.WriteLine($"[AUDIT ERROR] Failed to log audit entry: {ex.Message}");
            }
        }

        public async Task<IEnumerable<AuditLog>> GetRecentLogsAsync(int count = 100)
        {
            return await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToListAsync();
        }

        public async Task<IEnumerable<AuditLog>> GetLogsByUserAsync(string userId, DateTime? from = null, DateTime? to = null)
        {
            var query = _context.AuditLogs.Where(a => a.UserId == userId);

            if (from.HasValue)
                query = query.Where(a => a.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.Timestamp <= to.Value);

            return await query.OrderByDescending(a => a.Timestamp).ToListAsync();
        }

        public async Task<IEnumerable<AuditLog>> GetLogsByEntityAsync(string entityType, int entityId)
        {
            return await _context.AuditLogs
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetActionStatisticsAsync(DateTime from, DateTime to)
        {
            return await _context.AuditLogs
                .Where(a => a.Timestamp >= from && a.Timestamp <= to)
                .GroupBy(a => a.Action)
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Action, x => x.Count);
        }

        public async Task CleanupOldLogsAsync(int daysToKeep = 90)
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var oldLogs = await _context.AuditLogs
                .Where(a => a.Timestamp < cutoffDate)
                .ToListAsync();

            _context.AuditLogs.RemoveRange(oldLogs);
            await _context.SaveChangesAsync();

            Console.WriteLine($"Cleaned up {oldLogs.Count} audit logs older than {daysToKeep} days");
        }
    }
}
