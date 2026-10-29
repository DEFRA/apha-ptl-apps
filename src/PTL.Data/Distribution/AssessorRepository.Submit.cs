using Dapper;
using PTL.Core.Audit;
using PTL.Core.Distribution;
using PTL.Data.Audit;

namespace PTL.Data.Distribution;

// Legacy Assessor SubmitAssessment.DataPortal_Execute: sppSubmitAssessment. It stamps the test with the submission time and the
// Assessor (the procedure looks up the name with fnGetAssessorNameById), after which the assessment is final. The assignment and
// the not-yet-submitted check run in the same transaction, with the test row locked; the submission is also written to the
// existing tblLog (spiLog) in it, so a failed audit write rolls the submission back.
public sealed partial class AssessorRepository
{
    private const string SubmitSql = "EXEC dbo.sppSubmitAssessment @TestId = @TestId, @AssessmentSubmitted = @AssessmentSubmitted, @AssessorId = @AssessorId";

    public async Task<AssessorSubmitOutcome> SubmitAsync(Guid testId, Guid assessorId, DateTime submittedDate, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var isAssessor = await connection.ExecuteScalarAsync<int?>(AccessSql, new { TestId = testId, AssessorId = assessorId }, transaction);
            if (isAssessor is null)
            {
                transaction.Rollback();
                return AssessorSubmitOutcome.NotFound;
            }

            if (isAssessor == 0)
            {
                transaction.Rollback();
                return AssessorSubmitOutcome.Forbidden;
            }

            var submitted = await connection.ExecuteScalarAsync<DateTime?>(SubmittedSql, new { TestId = testId }, transaction);
            if (submitted is not null)
            {
                transaction.Rollback();
                return AssessorSubmitOutcome.AlreadySubmitted;
            }

            await connection.ExecuteAsync(SubmitSql, new { TestId = testId, AssessmentSubmitted = submittedDate, AssessorId = assessorId }, transaction);
            await AuditTrailWriter.WriteAsync(
                connection,
                transaction,
                new AuditEntry(assessorId, "SubmitAssessment", "DistributionTest", testId.ToString(), "Assessment submitted for the test"),
                LogPrefix,
                cancellationToken);

            transaction.Commit();
            return AssessorSubmitOutcome.Submitted;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
