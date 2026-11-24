using System.Data;
using Microsoft.EntityFrameworkCore;
using OOAD.Application.Common.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Domain.Interfaces;
using OOAD.Infrastructure.Persistence.Context;

namespace OOAD.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IRealtimeNotifier? _realtimeNotifier;
    private int _transactionDepth;
    private readonly List<Notification> _pendingNotifications = new();
    private bool _pendingNotifyStaffAdmin;
    private bool _pendingNotifyTrainers;

    public UnitOfWork(AppDbContext context, IRealtimeNotifier? realtimeNotifier = null)
    {
        _context = context;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        List<Notification> addedNotifications = new();
        bool notifyStaffAdmin = false;
        bool notifyTrainers = false;

        if (_realtimeNotifier != null)
        {
            var entries = _context.ChangeTracker.Entries().ToList();
            addedNotifications = entries
                .Where(e => e.State == EntityState.Added && e.Entity is Notification)
                .Select(e => (Notification)e.Entity)
                .ToList();

            notifyStaffAdmin = entries.Any(e =>
                (e.State == EntityState.Added || e.State == EntityState.Modified) &&
                (e.Entity is FreezeRequest || e.Entity is EquipmentIssue));

            notifyTrainers = entries.Any(e =>
                (e.State == EntityState.Added || e.State == EntityState.Modified) &&
                e.Entity is Booking booking &&
                booking.Type == BookingType.PersonalTrainer);
        }

        var affected = await _context.SaveChangesAsync(cancellationToken);

        if (_realtimeNotifier != null)
        {
            if (_transactionDepth > 0)
            {
                _pendingNotifications.AddRange(addedNotifications);
                _pendingNotifyStaffAdmin |= notifyStaffAdmin;
                _pendingNotifyTrainers |= notifyTrainers;
            }
            else
            {
                await DispatchRealtimeAsync(addedNotifications, notifyStaffAdmin, notifyTrainers, cancellationToken);
            }
        }

        return affected;
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var isOuter = _transactionDepth == 0;
        _transactionDepth++;
        await using var transaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            if (isOuter)
            {
                ClearPendingRealtime();
            }
            throw;
        }
        finally
        {
            _transactionDepth--;
        }

        if (isOuter)
        {
            await FlushPendingRealtimeAsync(cancellationToken);
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var isOuter = _transactionDepth == 0;
        _transactionDepth++;
        T result;
        await using var transaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try
        {
            result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            if (isOuter)
            {
                ClearPendingRealtime();
            }
            throw;
        }
        finally
        {
            _transactionDepth--;
        }

        if (isOuter)
        {
            await FlushPendingRealtimeAsync(cancellationToken);
        }

        return result;
    }

    private void ClearPendingRealtime()
    {
        _pendingNotifications.Clear();
        _pendingNotifyStaffAdmin = false;
        _pendingNotifyTrainers = false;
    }

    private async Task FlushPendingRealtimeAsync(CancellationToken cancellationToken)
    {
        if (_realtimeNotifier == null)
        {
            ClearPendingRealtime();
            return;
        }

        var notifications = _pendingNotifications.ToList();
        var notifyStaffAdmin = _pendingNotifyStaffAdmin;
        var notifyTrainers = _pendingNotifyTrainers;
        ClearPendingRealtime();

        await DispatchRealtimeAsync(notifications, notifyStaffAdmin, notifyTrainers, cancellationToken);
    }

    private async Task DispatchRealtimeAsync(
        List<Notification> addedNotifications,
        bool notifyStaffAdmin,
        bool notifyTrainers,
        CancellationToken cancellationToken)
    {
        if (_realtimeNotifier == null) return;

        foreach (var group in addedNotifications.GroupBy(n => n.UserId))
        {
            var latestForUser = group.Last();
            await _realtimeNotifier.NotifyUsersAsync(
                new[] { group.Key },
                latestForUser.Title,
                latestForUser.Content,
                cancellationToken);
        }

        var rolesToNotify = new List<string>();
        if (notifyStaffAdmin)
        {
            rolesToNotify.Add("STAFF");
            rolesToNotify.Add("ADMIN");
        }
        if (notifyTrainers)
        {
            rolesToNotify.Add("TRAINER");
        }
        if (rolesToNotify.Count > 0)
        {
            await _realtimeNotifier.NotifyRolesAsync(rolesToNotify, cancellationToken);
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
