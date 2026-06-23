using SE_Academic_Affairs_Support_System.Models;

namespace SE_Academic_Affairs_Support_System.Services.Email
{
    public enum TopicDecisionType { Approve, Revise, Reject }

    public interface IEmailService
    {
        Task SendConfirmRoomAsync(string toEmail, string fullName, TimeSpan startTime, TimeSpan endTime, DateTime bookingDate, string purPose);
        Task SendConfirmDeviceAsync(string toEmail, string fullName, DeviceRequest deviceRequest);
        Task SendConfirmAppAsync(string toEmail, string fullName, AppRegistrationRequest appRequest);

        Task<bool> SendTopicProposalToLecturerAsync(
            string toEmail, string lecturerName,
            string studentName, string studentCode,
            string topicTitle, string description,
            string reviewUrl);

        Task<bool> SendTopicDecisionToStudentAsync(
            string toEmail, string studentName,
            string topicTitle, TopicDecisionType decision,
            string? reason, string? actionUrl);
    }
}
