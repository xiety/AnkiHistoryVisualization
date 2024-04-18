﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AnkiDatabase;

public partial class AnkiDbContext(string filename) : DbContext
{
    public virtual DbSet<CardTable> Cards { get; set; }
    public virtual DbSet<DeckTable> Decks { get; set; }
    public virtual DbSet<NoteTable> Notes { get; set; }
    public virtual DbSet<RevlogTable> Revlogs { get; set; }
    public virtual DbSet<ColTable> Col { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"DataSource={filename}");
        optionsBuilder.AddInterceptors(new AnkiDbConnectionInterceptor());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ColTable>(entity =>
        {
            entity.Property(e => e.Crt)
                .HasConversion(
                    v => ((DateTimeOffset)v).ToUnixTimeSeconds(),
                    v => DateTimeOffset.FromUnixTimeSeconds(v).ToLocalTime().DateTime);
        });

        modelBuilder.Entity<CardTable>(entity =>
        {
            entity.HasOne(e => e.Deck).WithMany().HasForeignKey(a => a.Did);
            entity.HasOne(e => e.Note).WithMany().HasForeignKey(a => a.Nid);
        });

        modelBuilder.Entity<RevlogTable>(entity =>
        {
            entity.Property(e => e.Id)
                .HasConversion(
                    v => ((DateTimeOffset)v).ToUnixTimeMilliseconds(),
                    v => DateTimeOffset.FromUnixTimeMilliseconds(v).ToLocalTime().DateTime);

            entity.HasOne(e => e.Card).WithMany(a => a.Revlogs).HasForeignKey(a => a.Cid);
        });
    }
}

file class AnkiDbConnectionInterceptor : DbConnectionInterceptor
{
    public override DbConnection ConnectionCreated(ConnectionCreatedEventData eventData, DbConnection result)
    {
        var sqlite = (SqliteConnection)result;
        sqlite.CreateCollation("unicase", (a, b) => a.CompareTo(b));
        return sqlite;
    }
}

[Table("col")]
public class ColTable
{
    public long Id { get; set; }
    public DateTime Crt { get; set; }
}

[Table("cards")]
public partial class CardTable
{
    public long Id { get; set; }
    public long Nid { get; set; }
    public long Did { get; set; }
    public int Ord { get; set; }
    public int Queue { get; set; }
    public int Due { get; set; }
    public string Data { get; set; } = null!;

    public DeckTable Deck { get; set; } = default!;
    public NoteTable Note { get; set; } = default!;

    public ICollection<RevlogTable> Revlogs { get; set; } = default!;
}

[Table("decks")]
public partial class DeckTable
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
}

[Table("notes")]
public partial class NoteTable
{
    public long Id { get; set; }
    public string Flds { get; set; } = null!;
}

[Table("revlog")]
public partial class RevlogTable
{
    public DateTime Id { get; set; }
    public long Cid { get; set; }
    public int Ease { get; set; }
    public int Type { get; set; }

    public CardTable Card { get; set; } = default!;
}
