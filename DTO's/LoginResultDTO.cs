namespace ReStore___backend.Dtos
{
    public class LoginResultDTO
    {
        public required string Token { get; set; }
        public string? Email { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
