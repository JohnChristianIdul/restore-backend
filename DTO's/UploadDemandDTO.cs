namespace ReStore___backend.Dtos
{
    public class UploadDemandDTO
    {
        public string email { get; set; }
        public IFormFile File { get; set; }
    }
}
