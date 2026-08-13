using Microsoft.EntityFrameworkCore;

namespace ScanBot.Data
{
    public class AppDbContext : DbContext
    {
        public string DatabaseFilePath { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DatabaseFilePath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ImageRef>()
                .HasIndex(entity => entity.FolderName);
        }

        public DbSet<ImageRef> ImageRefs { get; set; }
    }
}
