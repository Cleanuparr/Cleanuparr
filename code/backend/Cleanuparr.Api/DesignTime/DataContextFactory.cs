using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Cleanuparr.Api.DesignTime;

public sealed class DataContextFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args) => DataContext.CreateStaticInstance();
}
