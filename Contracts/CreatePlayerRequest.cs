using System.ComponentModel.DataAnnotations;

namespace GameServerApi.Contracts;

public class CreatePlayerRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 20 characters.")]
    public string Name { get; set; } = "";

    [Range(1, 100, ErrorMessage = "Level must be between 1 and 100.")]
    public int Level { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Gold must be between 0 and 1000000.")]
    public int Gold { get; set; }
}
