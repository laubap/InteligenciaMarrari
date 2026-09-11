using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class GrupoAnaliseTagIa
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Grupo))]
    public int GrupoAnaliseIaId { get; set; }

    public GrupoAnaliseIa Grupo { get; set; } = null!;

    // Nome da tag.
    public string TagName { get; set; } = "";

    // Papel da tag dentro do grupo.
    //
    // Input
    // Target
    // Reference
    // Context
    //
    // Por enquanto será apenas informativo.
    public string Role { get; set; } = "Input";
}