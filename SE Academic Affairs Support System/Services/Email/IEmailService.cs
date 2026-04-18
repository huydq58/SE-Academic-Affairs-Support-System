using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services.Email
{
    public interface IEmailService
    {
        Task SendConfirmRoomAsync(string toEmail, string fullName,TimeSpan startTime,TimeSpan endTime, DateTime bookingDate, string purPose);
        Task SendConfirmDeviceAsync(string toEmail, string fullName, DeviceRequest deviceRequest);
        Task SendConfirmAppAsync(string toEmail, string fullName, AppRegistrationRequest appRequest);

    }
}
