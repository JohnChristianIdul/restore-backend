using Microsoft.AspNetCore.Mvc;
using ReStore___backend.Dtos;
using Restore_backend_deployment_.Models;
using System.Runtime.CompilerServices;

namespace ReStore___backend.Services.Interfaces
{
    public interface IDataService
    {
        Task ProcessAndUploadDataDemands(IEnumerable<dynamic> records, string email);
        Task<string> GetDemandDataFromStorageByEmail(string email);
        Task ProcessAndUploadDataSales(IEnumerable<dynamic> records, string email);
        Task<string> GetSalesDataFromStorageByEmail(string email);
        Task<string> SalesInsight(MemoryStream salesData, string email);
        Task<string> GetSalesInsightByEmail(string email);
        Task<string> TrainDemandModel(MemoryStream file, string email);
        Task<string> PredictDemand(string email);
        Task<string> TrainSalesModel(MemoryStream salesData, string email);
        Task<string> GetSalesPrediction(string email);
        Task<string> GetDemandPrediction(string email);
        Task<string> SignUp(string email, string name, string username, string phoneNumber, string password);
        Task SendVerificationEmailAsync(string email, string verificationLink);
        Task<(bool success, string message)> VerifyEmail(string oobcode);
        Task<LoginResultDTO> Login(string email, string password);
        Task SendPasswordResetEmailAsync(string email);
        Task SaveCustomerCreditsAsync(string email, int credits);
        Task DecreaseCreditsAsync(string email, int amount);
        Task SavePaymentReceiptAsync(PaymentReceipt receipt);
        Task<int> GetCustomerCreditsAsync(string email);
    }
}
