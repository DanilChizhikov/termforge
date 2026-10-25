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

            modelBuilder.Entity("DTech.Core.Operations.Operation", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<string>("Details")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("details");

                    b.Property<string>("Error")
                        .HasColumnType("TEXT")
                        .HasColumnName("error");

                    b.Property<DateTime?>("FinishedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("finished_at");

                    b.Property<string>("Kind")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("kind");

                    b.Property<bool>("Retryable")
                        .HasColumnType("INTEGER")
                        .HasColumnName("retryable");

                    b.Property<string>("Server")
                        .HasMaxLength(200)
                        .HasColumnType("TEXT")
                        .HasColumnName("server");

                    b.Property<DateTime?>("StartedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("started_at");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("TEXT")
                        .HasColumnName("status");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("updated_at");

                    b.HasKey("Id")
                        .HasName("pk_operations");

                    b.HasIndex("CreatedAt")
                        .HasDatabaseName("ix_operations_created_at");

                    b.HasIndex("Kind")
                        .HasDatabaseName("ix_operations_kind");

                    b.HasIndex("Server")
                        .HasDatabaseName("ix_operations_server");

                    b.HasIndex("Status")
                        .HasDatabaseName("ix_operations_status");

                    b.ToTable("operations", (string)null);
                });

            modelBuilder.Entity("DTech.Persistence.SavedConnections.SavedConnectionEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT")
                        .HasColumnName("id");

                    b.Property<string>("AuthMethod")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("TEXT")
                        .HasColumnName("auth_method");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<string>("GroupName")
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("group_name");

                    b.Property<bool>("HasPassword")
                        .HasColumnType("INTEGER")
                        .HasColumnName("has_password");

                    b.Property<string>("Host")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT")
                        .HasColumnName("host");

                    b.Property<Guid?>("JumpHostId")
                        .HasColumnType("TEXT")
                        .HasColumnName("jump_host_id");

                    b.Property<DateTime?>("LastConnectedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("last_connected_at");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .HasColumnName("name")
                        .UseCollation("NOCASE");

                    b.Property<int>("Port")
                        .HasColumnType("INTEGER")
                        .HasColumnName("port");

                    b.Property<string>("PrivateKeyPath")
                        .HasMaxLength(255)
                        .HasColumnType("TEXT")
                        .HasColumnName("private_key_path");

                    b.Property<string>("SshAlias")
                        .HasMaxLength(255)
                        .HasColumnType("TEXT")
                        .HasColumnName("ssh_alias");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("updated_at");

                    b.Property<string>("UserName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT")
                        .HasColumnName("user_name");

                    b.HasKey("Id")
                        .HasName("pk_saved_connections");

                    b.HasIndex("JumpHostId")
                        .HasDatabaseName("ix_saved_connections_jump_host_id");

                    b.HasIndex("Name")
                        .IsUnique()
                        .HasDatabaseName("ix_saved_connections_name");

                    b.ToTable("saved_connections", (string)null);
                });

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

            modelBuilder.Entity("DTech.Persistence.SavedConnections.SavedConnectionEntity", b =>
                {
                    b.HasOne("DTech.Persistence.SavedConnections.SavedConnectionEntity", null)
                        .WithMany()
                        .HasForeignKey("JumpHostId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .HasConstraintName("fk_saved_connections_saved_connections_jump_host_id");
                });
#pragma warning restore 612, 618
        }
    }
}
