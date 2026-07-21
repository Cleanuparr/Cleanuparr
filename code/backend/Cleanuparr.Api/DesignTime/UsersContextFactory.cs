using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace Cleanuparr.Api.DesignTime;

public sealed class UsersContextFactory : IDesignTimeDbContextFactory<UsersContext>
{
    public UsersContext CreateDbContext(string[] args) => UsersContext.CreateStaticInstance();
}
