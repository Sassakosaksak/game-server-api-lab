using System.ComponentModel.DataAnnotations;

namespace GameServerApi.Contracts;

public class LoginRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "PlayerId must be 1 or greater.")]
    public int PlayerId { get; set; }
}
