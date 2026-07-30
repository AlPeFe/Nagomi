using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Nagomi.Api.Features.ReferenceData;

namespace Nagomi.UnitTests.ReferenceData;

internal sealed class TestNagomiDb : INagomiDb
{
    public TestDbSet<IneAutonomousCommunity> Communities { get; } = [];
    public TestDbSet<IneProvince> Provinces { get; } = [];
    public TestDbSet<IneMunicipality> Municipalities { get; } = [];
    public TestDbSet<TransportReason> Reasons { get; } = [];
    public TestDbSet<HealthcareFacility> Facilities { get; } = [];

    public DbSet<IneAutonomousCommunity> IneAutonomousCommunities => Communities;
    public DbSet<IneProvince> IneProvinces => Provinces;
    public DbSet<IneMunicipality> IneMunicipalities => Municipalities;
    public DbSet<TransportReason> TransportReasons => Reasons;
    public DbSet<HealthcareFacility> HealthcareFacilities => Facilities;

    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveChangesCalls++;
        return Task.FromResult(0);
    }
}

internal sealed class TestDbSet<T> : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>
    where T : class
{
    private readonly List<T> items = [];

    public override IEntityType EntityType => null!;

    public void Seed(params T[] entities) => items.AddRange(entities);

    public override EntityEntry<T> Add(T entity)
    {
        items.Add(entity);
        return null!;
    }

    Type IQueryable.ElementType => typeof(T);
    Expression IQueryable.Expression => items.AsQueryable().Expression;
    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(items.AsQueryable().Provider);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(items.GetEnumerator(), cancellationToken);
}

internal sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => inner.Execute(expression);
    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var result = typeof(IQueryProvider)
            .GetMethods()
            .Single(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
            .MakeGenericMethod(resultType)
            .Invoke(inner, [expression]);
        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [result])!;
    }
}

internal sealed class TestAsyncEnumerable<T>(Expression expression) : EnumerableQuery<T>(expression),
    IAsyncEnumerable<T>, IQueryable<T>
{
    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new TestAsyncEnumerator<T>(((IEnumerable<T>)this).GetEnumerator(), cancellationToken);
}

internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner, CancellationToken cancellationToken)
    : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask<bool> MoveNextAsync()
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(inner.MoveNext());
    }

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }
}
