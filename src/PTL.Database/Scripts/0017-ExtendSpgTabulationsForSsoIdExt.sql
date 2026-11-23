-- Extends spgTabulations (ExternalWeb "View reports") to identify the caller by the CIDM contact id (fldSsoIdExt), the
-- same identity sign-in uses (0013), instead of the legacy fldSsoId - which the new application never maintains, so a
-- Viewer or External Test Consultant created after migration has fldSsoId = '00000000-...' and matched every other
-- record with that empty id.
--
-- @SsoIdExt (+ @ParticipantId for the selected lab of a Participant or Lab User) is appended as the LAST parameters so
-- the existing legacy call (EXEC spgTabulations @SsoId) keeps working unchanged in the ELSE branch; that branch can be
-- deleted once the legacy application is retired.
--
-- With @SsoIdExt:
--   * External Test Consultant: tblExternalTestConsultant.fldSsoIdExt, active, allocated as TC 1/2/3 on the scheme.
--   * Viewer: tblViewer.fldSsoIdExt, linked to the scheme (tlnkViewerScheme) or to a participant in the distribution
--     (tlnkViewerParticipant).
--   * Participant / Lab User: @ParticipantId only counts when it is an active lab this contact id owns (Participant) or
--     is actively mapped to (Lab User), and the lab was in the distribution.
--   Availability, latest-version, not-cancelled and seven-year rules are the same as the legacy branch.
--
-- Safe to re-run.

-- A procedure keeps the QUOTED_IDENTIFIER setting it was created with (see 0013).
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON

GO

CREATE OR ALTER PROCEDURE [dbo].[spgTabulations]
	@SsoId uniqueidentifier = NULL,
	@ReferenceDate datetime = NULL,
	@SsoIdExt uniqueidentifier = NULL,
	@ParticipantId uniqueidentifier = NULL
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @SevenYearsAgoDate DATE = CAST(dbo.fnGetSevenYearsAgo(@ReferenceDate) AS date)

	IF @SsoIdExt IS NOT NULL
	BEGIN
		DECLARE @ExtTestConsultantId uniqueidentifier
		SELECT @ExtTestConsultantId = fldExternalTestConsultantId
		FROM tblExternalTestConsultant
		WHERE fldSsoIdExt = @SsoIdExt AND fldIsInactive = 0

		DECLARE @ViewerId uniqueidentifier
		SELECT @ViewerId = fldViewerId
		FROM tblViewer
		WHERE fldSsoIdExt = @SsoIdExt

		DECLARE @LabParticipantId uniqueidentifier
		SELECT @LabParticipantId = p.fldParticipantId
		FROM tblParticipant p
		WHERE p.fldParticipantId = @ParticipantId
			AND p.fldIsActive = 1
			AND
			(
				p.fldSsoIdExt = @SsoIdExt
				OR EXISTS
				(
					SELECT 1
					FROM tblLabUser u
					INNER JOIN tlnkLabUserParticipant m ON m.fldLabUserId = u.fldLabUserId
					WHERE u.fldSsoIdExt = @SsoIdExt
						AND m.fldParticipantId = p.fldParticipantId
						AND m.fldIsActive = 1
				)
			)

		SELECT
			pt.fldPublishedTabulationId fldTabulationId,
			pt.fldDistribution fldDistributionReference,
			pt.fldYearId,
			pt.fldMonthId,
			pt.fldSchemeIdentifier + ': ' + pt.fldSchemeName fldSchemeName,
			pt.fldName fldTabulationName,
			pt.fldDistributionDate
		FROM
			tblPublishedTabulation pt
			INNER JOIN tblDistributionTabulation dt ON pt.fldDistributionTabulationId = dt.fldDistributionTabulationId
			INNER JOIN tblMonthlyDistributionScheme mds ON mds.fldMonthlyDistributionSchemeId = dt.fldMonthlyDistributionSchemeId
			INNER JOIN tblScheme s ON s.fldSchemeId = mds.fldSchemeId
		WHERE
			mds.fldIsCancelled = 0
			AND CAST(mds.fldResultsIssueTargetDate AS date) > @SevenYearsAgoDate
			AND pt.fldCreationDate =
			(
				SELECT MAX(p2.fldCreationDate)
				FROM tblPublishedTabulation p2
				WHERE p2.fldDistributionTabulationId = dt.fldDistributionTabulationId
			)
			AND
			(
				(
					pt.fldAvailableToViewers = 1
					AND
					(
						@ExtTestConsultantId IN (s.fldTestConsultant1, s.fldTestConsultant2, s.fldTestConsultant3)
						OR EXISTS
						(
							SELECT 1
							FROM tlnkViewerScheme vs
							WHERE vs.fldSchemeId = s.fldSchemeId AND vs.fldViewerId = @ViewerId
						)
						OR EXISTS
						(
							SELECT 1
							FROM tlnkDistributionParticipant dp
							INNER JOIN tlnkViewerParticipant vp ON vp.fldParticipantId = dp.fldParticipantId
							WHERE dp.fldDistributionSchemeId = mds.fldMonthlyDistributionSchemeId AND vp.fldViewerId = @ViewerId
						)
					)
				)
				OR
				(
					pt.fldAvailableToParticipants = 1
					AND EXISTS
					(
						SELECT 1
						FROM tlnkDistributionParticipant dp
						WHERE dp.fldDistributionSchemeId = mds.fldMonthlyDistributionSchemeId AND dp.fldParticipantId = @LabParticipantId
					)
				)
			)
		ORDER BY
			fldDistributionReference
	END
	ELSE
	BEGIN
		--If the SsoId is an external TC, get their internal ID
		DECLARE @TestConsultantId uniqueidentifier
		SELECT @TestConsultantId = fldExternalTestConsultantId
		FROM tblExternalTestConsultant
		WHERE fldSsoId = @SsoId

		SELECT
			tblPublishedTabulation.fldPublishedTabulationId fldTabulationId,
			tblPublishedTabulation.fldDistribution fldDistributionReference,
			tblPublishedTabulation.fldYearId,
			tblPublishedTabulation.fldMonthId,
			tblPublishedTabulation.fldSchemeIdentifier + ': ' + tblPublishedTabulation.fldSchemeName fldSchemeName,
			tblPublishedTabulation.fldName fldTabulationName,
			tblPublishedTabulation.fldDistributionDate
		FROM
			tblPublishedTabulation
			INNER JOIN tblDistributionTabulation ON tblPublishedTabulation.fldDistributionTabulationId = tblDistributionTabulation.fldDistributionTabulationId
			INNER JOIN tblMonthlyDistributionScheme ON tblMonthlyDistributionScheme.fldMonthlyDistributionSchemeId = tblDistributionTabulation.fldMonthlyDistributionSchemeId
			INNER JOIN tblScheme ON tblScheme.fldSchemeId = tblMonthlyDistributionScheme.fldSchemeId
			LEFT JOIN tlnkViewerScheme ON tlnkViewerScheme.fldSchemeId = tblScheme.fldSchemeId
			LEFT JOIN tblViewer tblViewer1 ON tlnkViewerScheme.fldViewerId = tblViewer1.fldViewerId
			LEFT JOIN tlnkDistributionParticipant ON tblMonthlyDistributionScheme.fldMonthlyDistributionSchemeId = tlnkDistributionParticipant.fldDistributionSchemeId
			LEFT JOIN tblParticipant ON tlnkDistributionParticipant.fldParticipantId = tblParticipant.fldParticipantId
			LEFT JOIN tlnkViewerParticipant ON tblParticipant.fldParticipantId = tlnkViewerParticipant.fldParticipantId
			LEFT JOIN tblViewer tblViewer2 ON tlnkViewerParticipant.fldViewerId = tblViewer2.fldViewerId
		WHERE
			(
				(
					(
						tblViewer1.fldSsoId = @SsoId
						OR tblViewer2.fldSsoId = @SsoId
						OR tblScheme.fldTestConsultant1 = @TestConsultantId
						OR tblScheme.fldTestConsultant2 = @TestConsultantId
						OR tblScheme.fldTestConsultant3 = @TestConsultantId
					)
					AND tblPublishedTabulation.fldAvailableToViewers = 1
				)
				OR
				(
					tblParticipant.fldSsoId = @SsoId
					AND tblPublishedTabulation.fldAvailableToParticipants = 1
				)
			)
			AND -- check it is the most recent
			(
				(
					SELECT Max(fldCreationDate)
					FROM tblPublishedTabulation
					WHERE fldDistributionTabulationId = tblDistributionTabulation.fldDistributionTabulationId
				) = tblPublishedTabulation.fldCreationDate
			)
			AND
			(
			tblMonthlyDistributionScheme.fldIsCancelled = 0
			)
			AND
			(
				CAST(tblMonthlyDistributionScheme.fldResultsIssueTargetDate AS date) > @SevenYearsAgoDate
			)
		GROUP BY
			tblPublishedTabulation.fldPublishedTabulationId,
			tblPublishedTabulation.fldDistribution,
			tblPublishedTabulation.fldYearId,
			tblPublishedTabulation.fldMonthId,
			tblPublishedTabulation.fldSchemeIdentifier + ': ' + tblPublishedTabulation.fldSchemeName,
			tblPublishedTabulation.fldName,
			tblPublishedTabulation.fldDistributionDate
		ORDER BY
			fldDistributionReference
	END
END

GO
GRANT EXECUTE ON [dbo].[spgTabulations] TO [ProficiencyTestingInternalUser]
GO
