using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.ViewModels;

namespace SE_Academic_Affairs_Support_System.Services.ProjectRegistration
{
    public interface IRegistrationService
    {
        // Period
        Task<RegistrationPeriod?> GetActivePeriodAsync();
        Task<List<RegistrationPeriod>> GetAllPeriodsAsync();
        Task CreatePeriodAsync(PeriodFormViewModel vm);
        Task UpdatePeriodAsync(PeriodFormViewModel vm);
        Task SetPeriodActiveAsync(int periodId);
        Task ClosePeriodAndAutoRejectPendingAsync(int periodId);

        // Topics (Lecturer)
        Task<List<TopicManageRow>> GetLecturerTopicsAsync(int lecturerProfileId);
        Task CreateTopicAsync(CreateTopicViewModel vm, int lecturerProfileId);
        Task DeleteTopicAsync(int topicId, int lecturerProfileId);

        // Browse topics (Student)
        Task<TopicListViewModel?> GetTopicListForStudentAsync(
            int studentProfileId, string? keyword, int? lecturerId);

        // Flow A: Register existing topic
        Task<(bool Success, string Message)> RegisterExistingTopicAsync(
            int studentProfileId, int topicId);

        // Flow B: Propose new topic
        Task<(bool Success, string Message)> SubmitProposalAsync(
            int studentProfileId, ProposalViewModel vm);

        Task<(bool Success, string Message)> ResubmitProposalAsync(
            int studentProfileId, int registrationId, ProposalViewModel vm);

        Task<(bool Success, string Message)> CancelRegistrationAsync(
            int studentProfileId, int registrationId);

        // My registrations
        Task<MyRegistrationsViewModel> GetMyRegistrationsAsync(int studentProfileId);
        Task<ProposalViewModel?> GetProposalForRevisionAsync(int registrationId, int studentProfileId);

        // Lecturer inbox
        Task<LecturerInboxViewModel> GetLecturerInboxAsync(int lecturerProfileId);
        Task<ReviewDecisionViewModel?> GetProposalForReviewAsync(int registrationId, int lecturerProfileId);
        Task<(bool Success, string Message)> ProcessDecisionAsync(
            int lecturerProfileId, ReviewDecisionViewModel vm);

        // Admin export
        Task<List<ExportRowViewModel>> GetExportDataAsync(int periodId);
    }
}
