namespace ReStore___backend.Dtos
{
    public class LoginResultDTO
    {
        public required string Token { get; set; }
        public string? Username { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
