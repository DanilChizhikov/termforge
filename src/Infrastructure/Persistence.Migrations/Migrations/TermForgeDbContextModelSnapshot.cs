using System;
using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DTech.Persistence.Migrations
{
    [DbContext(typeof(TermForgeDbContext))]
    partial class TermForgeDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "10.0.10");

            modelBuilder.Entity("DTech.Persistence.Settings.AppSetting", b =>
                {
                    b.Property<string>("Key")
                        .HasMaxLength(200)
                        .HasColumnType("TEXT")
                        .HasColumnName("key");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("updated_at");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("value");

                    b.HasKey("Key")
                        .HasName("pk_app_settings");

                    b.ToTable("app_settings", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}
