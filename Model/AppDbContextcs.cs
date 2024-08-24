using Microsoft.EntityFrameworkCore;

namespace PaystackIntegration.Model
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
		{

		}

		public DbSet<Transaction> Transactions { get; set; }
	}
}