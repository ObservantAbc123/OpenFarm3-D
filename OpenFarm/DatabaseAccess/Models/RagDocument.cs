using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace DatabaseAccess.Models;

[Table("rag_documents")]
public partial class RagDocument
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("embedding", TypeName = "vector(4096)")] // llama3 generates 4096-dimensional embeddings
    public Vector? Embedding { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
