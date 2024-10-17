using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Dtos;
using System.Runtime.CompilerServices;

namespace ReStore___backend.Services.Interfaces
{
    public interface IDataService
    {
        Task ProcessAndUploadDataDemands(IEnumerable<dynamic> records, string username);
        Task<string> GetDemandDataFromStorageByUsername(string username);
        Task ProcessAndUploadDataSales(IEnumerable<dynamic> records, string username);
        Task<string> GetSalesDataFromStorageByUsername(string username);
        Task<string> SalesInsight(MemoryStream salesData, string username);
        Task<string> GetSalesInsightByUsername(string username);
        Task<string> TrainDemandModel(MemoryStream file, string username);
        Task<string> PredictDemand(string username);
        Task<string> TrainSalesModel(MemoryStream salesData, string username);
        Task<string> GetSalesPrediction(string username);
        Task<string> GetDemandPrediction(string username);
        Task<string> SignUp(string email, string name, string username, string phoneNumber, string password);
        Task SendVerificationEmailAsync(string email, string verificationLink);
        Task<string> VerifyEmail(string oobcode, string userId);
        Task<bool> IsEmailVerified(string userId);
        Task<LoginResultDTO> Login(string email, string password);
        Task SendPasswordResetEmailAsync(string email);
    }
}
