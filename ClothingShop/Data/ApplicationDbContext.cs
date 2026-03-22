using Microsoft.EntityFrameworkCore;
using ClothingShop.Models;

namespace ClothingShop.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {

        // DB SETS
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<ProductReview> ProductReviews { get; set; } = null!;
        public DbSet<PaymentInfo> PaymentInfos { get; set; } = null!;
        public DbSet<FashionCategory> FashionCategories { get; set; } = null!;
        public DbSet<ProductCategory> ProductCategories { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<WishlistItem> WishlistItems { get; set; } = null!;
        public DbSet<Cart> Carts { get; set; } = null!;
        public DbSet<SupportTicket> SupportTickets { get; set; } = null!;
        public DbSet<SupportMessage> SupportMessages { get; set; } = null!;
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
        public DbSet<ProductView> ProductViews { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name)
                      .IsRequired()
                      .HasMaxLength(200);
                entity.Property(p => p.Price)
                      .HasColumnType("decimal(18,2)")
                      .IsRequired();
                entity.Property(p => p.ImageUrl)
                      .HasMaxLength(500);
                entity.Property(p => p.Color)
                      .HasMaxLength(500);
                entity.Property(p => p.Size)
                      .HasMaxLength(200);
                entity.Property(p => p.Quantity)
                      .IsRequired()
                      .HasDefaultValue(0);
                entity.Property(p => p.Description)
                      .HasMaxLength(1000);
                entity.Property(p => p.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.FullName)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(u => u.Email)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.HasIndex(u => u.Email)
                      .IsUnique();
                entity.Property(u => u.PasswordHash)
                      .IsRequired()
                      .HasMaxLength(256);
                entity.Property(u => u.PhoneNumber)
                      .IsRequired()
                      .HasMaxLength(20);
                entity.Property(u => u.Gender)
                      .HasMaxLength(10);
                entity.Property(u => u.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(u => u.IsAdmin)
                      .HasDefaultValue(false);
            });

            // C?U HÌNH ORDER
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(o => o.Status).HasMaxLength(20);
                entity.Property(o => o.OrderDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(o => o.FullName).HasMaxLength(100);
                entity.Property(o => o.PhoneNumber).HasMaxLength(20);
                entity.Property(o => o.Address).HasMaxLength(500);
                entity.Property(o => o.Note).HasMaxLength(1000);
                entity.Property(o => o.CancelReason).HasMaxLength(500);
                entity.Property(o => o.CancelledBy).HasMaxLength(20);
            });

            // C?U HÌNH ORDERITEM – S?A TÊN BI?N 'oi'
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(oi => oi.Id);
                entity.Property(oi => oi.Price).HasColumnType("decimal(18,2)"); 
                entity.Property(oi => oi.ProductName).HasMaxLength(200);       
            });

            // C?U HÌNH PRODUCTREVIEW
            modelBuilder.Entity<ProductReview>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Rating).IsRequired();
                entity.Property(r => r.Comment).HasMaxLength(1000);
                entity.Property(r => r.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                // Relationships
                entity.HasOne(r => r.Product)
                      .WithMany()
                      .HasForeignKey(r => r.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(r => r.User)
                      .WithMany()
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(r => r.Order)
                      .WithMany()
                      .HasForeignKey(r => r.OrderId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // C?U HÌNH PAYMENTINFO
            modelBuilder.Entity<PaymentInfo>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.BankName).HasMaxLength(100);
                entity.Property(p => p.BankAccountNumber).HasMaxLength(50);
                entity.Property(p => p.BankAccountName).HasMaxLength(100);
                entity.Property(p => p.MoMoPhone).HasMaxLength(20);
                entity.Property(p => p.MoMoName).HasMaxLength(100);
                entity.Property(p => p.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // C?U HÌNH FASHIONCATEGORY
            modelBuilder.Entity<FashionCategory>(entity =>
            {
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Title).IsRequired().HasMaxLength(100);
                entity.Property(f => f.ImageUrl).IsRequired().HasMaxLength(500);
                entity.Property(f => f.ImageUrlAvif).HasMaxLength(500);
                entity.Property(f => f.LinkUrl).HasMaxLength(200);
                entity.Property(f => f.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // C?U HÌNH PRODUCTCATEGORY
            modelBuilder.Entity<ProductCategory>(entity =>
            {
                entity.HasKey(pc => pc.Id);
                entity.Property(pc => pc.Name).IsRequired().HasMaxLength(100);
                entity.Property(pc => pc.Description).HasMaxLength(500);
                entity.Property(pc => pc.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // C?U HÌNH NOTIFICATION
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Title).IsRequired().HasMaxLength(200);
                entity.Property(n => n.Message).IsRequired().HasMaxLength(1000);
                entity.Property(n => n.Type).HasMaxLength(20);
                entity.Property(n => n.IsRead).HasDefaultValue(false);
                entity.Property(n => n.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(n => n.User)
                      .WithMany()
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // C?U HÌNH WISHLISTITEM
            modelBuilder.Entity<WishlistItem>(entity =>
            {
                entity.HasKey(w => w.Id);
                entity.Property(w => w.AddedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(w => w.User)
                      .WithMany()
                      .HasForeignKey(w => w.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(w => w.Product)
                      .WithMany()
                      .HasForeignKey(w => w.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                // Ð?m b?o m?i user ch? có 1 wishlist item cho m?i product
                entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
            });

            // C?U HÌNH CART
            modelBuilder.Entity<Cart>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Quantity).IsRequired();
                entity.Property(c => c.Size).HasMaxLength(20);
                entity.Property(c => c.Color).HasMaxLength(50);
                entity.Property(c => c.AddedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(c => c.User)
                      .WithMany()
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(c => c.Product)
                      .WithMany()
                      .HasForeignKey(c => c.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                // Ð?m b?o m?i user ch? có 1 cart item cho m?i product+size+color
                entity.HasIndex(c => new { c.UserId, c.ProductId, c.Size, c.Color }).IsUnique();
            });

            // C?U HÌNH SUPPORT TICKET
            modelBuilder.Entity<SupportTicket>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Subject).IsRequired().HasMaxLength(200);
                entity.Property(t => t.Status).IsRequired().HasMaxLength(20);
                entity.Property(t => t.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(t => t.User)
                      .WithMany()
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // C?U HÌNH SUPPORT MESSAGE
            modelBuilder.Entity<SupportMessage>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Message).IsRequired().HasMaxLength(2000);
                entity.Property(m => m.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(m => m.Ticket)
                      .WithMany(t => t.Messages)
                      .HasForeignKey(m => m.TicketId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(m => m.Sender)
                      .WithMany()
                      .HasForeignKey(m => m.SenderId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // C?U HÌNH INVENTORY TRANSACTION
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasKey(it => it.Id);
                entity.Property(it => it.Type).IsRequired().HasMaxLength(20);
                entity.Property(it => it.Quantity).IsRequired();
                entity.Property(it => it.Reason).HasMaxLength(500);
                entity.Property(it => it.Supplier).HasMaxLength(100);
                entity.Property(it => it.Cost).HasColumnType("decimal(18,2)");
                entity.Property(it => it.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(it => it.Product)
                      .WithMany()
                      .HasForeignKey(it => it.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(it => it.Creator)
                      .WithMany()
                      .HasForeignKey(it => it.CreatedBy)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(it => it.Order)
                      .WithMany()
                      .HasForeignKey(it => it.OrderId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // C?U HÌNH PRODUCT VIEW
            modelBuilder.Entity<ProductView>(entity =>
            {
                entity.HasKey(pv => pv.Id);
                entity.Property(pv => pv.SessionId).HasMaxLength(50);
                entity.Property(pv => pv.ViewedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(pv => pv.User)
                      .WithMany()
                      .HasForeignKey(pv => pv.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(pv => pv.Product)
                      .WithMany()
                      .HasForeignKey(pv => pv.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                // Index d? tìm ki?m nhanh
                entity.HasIndex(pv => new { pv.UserId, pv.ViewedAt });
                entity.HasIndex(pv => new { pv.SessionId, pv.ViewedAt });
            });

            base.OnModelCreating(modelBuilder); 
        }
    }
}
