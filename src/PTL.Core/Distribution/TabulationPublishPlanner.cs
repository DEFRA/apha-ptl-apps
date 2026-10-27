namespace PTL.Core.Distribution;

// One call of an existing legacy stored procedure (spiPublished...), with the parameters legacy passed it.
public sealed record PublishCommand(string Procedure, IReadOnlyList<KeyValuePair<string, object?>> Parameters)
{
    // "EXEC dbo.spiPublishedSample @PublishedSampleId = @PublishedSampleId, ..." - the values travel as parameters.
    public string CommandText => $"EXEC dbo.{Procedure} " + string.Join(", ", Parameters.Select(p => $"@{p.Key} = @{p.Key}"));
}

public sealed record TabulationPublishPlan(Guid PublishedTabulationId, IReadOnlyList<PublishCommand> Commands);

// Legacy Published/PublishTabulation.vb: copies the draft tabulation structure into the published tables, in the same
// order and with the same new GUIDs-per-row, using the same procedures. Pure, so the mapping can be checked without a
// database; the data layer runs the commands in one transaction.
public static class TabulationPublishPlanner
{
    public static TabulationPublishPlan Plan(TabulationPublishSource source, string signatory, DateTime creationDate, Func<Guid>? newId = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(signatory);

        return new Builder(source, signatory, creationDate, newId ?? Guid.NewGuid).Build();
    }

    private sealed class Builder(TabulationPublishSource source, string signatory, DateTime creationDate, Func<Guid> newId)
    {
        private const string PublishedTabulationIdName = "PublishedTabulationId";
        private const string PublishedSampleIdName = "PublishedSampleId";
        private const string PublishedTestIdName = "PublishedTestId";
        private const string PublishedParticipantIdName = "PublishedParticipantId";
        private const string OrderName = "Order";
        private const string ResultName = "Result";

        private readonly List<PublishCommand> _commands = [];

        // Draft id -> published id, for every row later rows refer back to (legacy mGuidDictionary).
        private readonly Dictionary<Guid, Guid> _published = [];

        private readonly Guid _tabulationId = newId();

        public TabulationPublishPlan Build()
        {
            SaveTabulation();
            SaveTests();
            SaveIntendedResults();
            SaveParticipants();
            SaveAttachments();
            return new TabulationPublishPlan(_tabulationId, _commands);
        }

        private Guid PublishedId(Guid draftId) =>
            _published.TryGetValue(draftId, out var id)
                ? id
                : throw new InvalidOperationException($"The draft row {draftId} is not part of the tabulation being published.");

        private Guid Register(Guid draftId)
        {
            var id = newId();
            _published[draftId] = id;
            return id;
        }

        private void Add(string procedure, params (string Name, object? Value)[] parameters) =>
            _commands.Add(new PublishCommand(procedure, [.. parameters.Select(p => KeyValuePair.Create(p.Name, p.Value))]));

        private void SaveTabulation()
        {
            var h = source.Header;
            _published[h.DistributionTabulationId] = _tabulationId;

            Add(
                "spiPublishedTabulation",
                (PublishedTabulationIdName, _tabulationId),
                ("DistributionTabulationId", h.DistributionTabulationId),
                ("Name", h.TabulationName),
                ("Signatory", signatory),
                ("CreationDate", creationDate),
                ("DistributionDate", h.DistributionDate),
                ("Distribution", h.DistributionReference + h.DistributionReferenceSuffix + "/" + h.ScheduleCode),
                ("SchemeIdentifier", h.SchemeIdentifier),
                ("SchemeName", h.SchemeName),
                ("MonthId", source.Scheme.MonthId),
                ("YearId", source.Scheme.YearId),
                ("SampleComments", h.Comments),
                ("TestConsultantComments", h.TestConsultantComments),
                ("TestConsultantCommentsAuthor", h.TestConsultantCommentsAuthor),
                ("TestConsultantCommentsDept", h.TestConsultantCommentsDept),
                ("IsAccredited", source.Scheme.IsAccredited),
                ("SchemeVersionDate", h.SchemeVersionDate),
                ("HasRatings", h.HasRatings),
                ("IntendedResultsOnly", h.IntendedResultsOnly),
                ("SingleParticipantTabulation", h.SingleParticipantTabulation),
                ("AvailableToParticipants", h.AvailableToParticipants),
                ("AvailableToViewers", h.AvailableToViewers),
                ("RequiresAssessment", h.RequiresAssessment),
                ("StandardTabulationText", h.StandardTabulationText));
        }

        private void SaveTests()
        {
            foreach (var sample in source.Samples)
            {
                Add(
                    "spiPublishedSample",
                    (PublishedSampleIdName, Register(sample.SampleId)),
                    ("SampleId", sample.SampleId),
                    ("SampleNumber", sample.SampleNumber),
                    ("TabulationDescription", sample.TabulationDescription));
            }

            var testOrder = 1;
            foreach (var test in source.Tests)
            {
                var testId = Register(test.TestId);
                Add(
                    "spiPublishedTest",
                    (PublishedTestIdName, testId),
                    (PublishedTabulationIdName, _tabulationId),
                    ("DistributionTestId", test.TestId),
                    ("Name", test.Name),
                    (OrderName, testOrder),
                    ("AssessmentSubmitted", test.AssessmentSubmitted),
                    ("AssessorId", test.AssessorId),
                    ("AssessorName", test.AssessorName));

                foreach (var sampleTest in test.Samples)
                {
                    Add(
                        "spiPublishedSampleTest",
                        ("PublishedSampleTestId", Register(sampleTest.SampleTestId)),
                        (PublishedSampleIdName, PublishedId(sampleTest.SampleId)),
                        (PublishedTestIdName, testId));
                }

                SaveOrderedItems("spiPublishedTestResultItem", "PublishedTestResultItemId", "DistributionTestResultItemId", testId, test.ResultItems);
                SaveOrderedItems("spiPublishedTestMethodItem", "PublishedTestMethodItemId", "DistributionTestMethodItemId", testId, test.MethodItems);

                if (source.Scheme.RequiresAssessment)
                {
                    SaveCategoryItems(testId, test.CategoryItems);
                }

                testOrder++;
            }
        }

        private void SaveOrderedItems(string procedure, string publishedIdName, string draftIdName, Guid testId, IReadOnlyList<PublishNamedItem> items)
        {
            var order = 1;
            foreach (var item in items)
            {
                Add(
                    procedure,
                    (publishedIdName, Register(item.Id)),
                    (PublishedTestIdName, testId),
                    (draftIdName, item.Id),
                    ("Name", item.Name),
                    (OrderName, order));
                order++;
            }
        }

        private void SaveCategoryItems(Guid testId, IReadOnlyList<PublishCategoryItem> categoryItems)
        {
            var categoryOrder = 1;
            foreach (var category in categoryItems)
            {
                var categoryId = Register(category.CategoryItemId);
                Add(
                    "spiPublishedCategoryItem",
                    ("PublishedCategoryItemId", categoryId),
                    (PublishedTestIdName, testId),
                    ("DistributionCategoryItemId", category.CategoryItemId),
                    ("Name", category.Name),
                    (OrderName, categoryOrder));

                var criterionOrder = 1;
                foreach (var criterion in category.Criteria)
                {
                    Add(
                        "spiPublishedCriterionItem",
                        ("PublishedCriterionItemId", Register(criterion.Id)),
                        ("PublishedCategoryItemId", categoryId),
                        ("DistributionCriterionItemId", criterion.Id),
                        ("Name", criterion.Name),
                        (OrderName, criterionOrder));
                    criterionOrder++;
                }

                categoryOrder++;
            }
        }

        private void SaveIntendedResults()
        {
            foreach (var method in source.IntendedMethods)
            {
                Add(
                    "spiPublishedIntendedMethodItem",
                    ("PublishedIntendedMethodItemId", newId()),
                    (PublishedTabulationIdName, _tabulationId),
                    ("PublishedTestMethodItemId", PublishedId(method.TestMethodItemId)),
                    (ResultName, method.Result));
            }

            foreach (var result in source.IntendedResults)
            {
                Add(
                    "spiPublishedIntendedResultItem",
                    ("PublishedIntendedResultItemId", newId()),
                    (PublishedTabulationIdName, _tabulationId),
                    ("PublishedTestResultItemId", PublishedId(result.TestResultItemId)),
                    (PublishedSampleIdName, PublishedId(result.SampleId)),
                    (ResultName, result.Result),
                    ("Result2", result.Result2));
            }
        }

        private void SaveParticipants()
        {
            foreach (var participant in source.Participants)
            {
                var participantId = Register(participant.DistributionParticipantId);
                Add(
                    "spiPublishedParticipant",
                    (PublishedParticipantIdName, participantId),
                    (PublishedTabulationIdName, _tabulationId),
                    ("DistributionParticipantId", participant.DistributionParticipantId),
                    ("LabCode", participant.LabCode),
                    ("NilReturnReason", participant.NilReturnReason));

                if (source.Header.HasRatings)
                {
                    foreach (var rating in participant.Ratings)
                    {
                        Add(
                            "spiPublishedSampleRating",
                            (PublishedParticipantIdName, participantId),
                            (PublishedSampleIdName, PublishedId(rating.SampleId)),
                            ("Rating", rating.Rating));
                    }
                }

                foreach (var method in participant.MethodResults)
                {
                    Add(
                        "spiPublishedParticipantMethodItem",
                        ("PublishedParticipantMethodItemId", newId()),
                        (PublishedParticipantIdName, participantId),
                        ("PublishedTestMethodItemId", PublishedId(method.TestMethodItemId)),
                        (ResultName, method.Result));
                }

                foreach (var result in participant.ResultItemResults)
                {
                    Add(
                        "spiPublishedParticipantResultItem",
                        ("PublishedParticipantResultItemId", newId()),
                        (PublishedParticipantIdName, participantId),
                        ("PublishedTestResultItemId", PublishedId(result.TestResultItemId)),
                        (PublishedSampleIdName, PublishedId(result.SampleId)),
                        (ResultName, result.Result));
                }

                if (source.Scheme.RequiresAssessment)
                {
                    SaveAssessment(participantId, participant);
                }
            }
        }

        private void SaveAssessment(Guid participantId, PublishParticipant participant)
        {
            foreach (var assessment in participant.Assessments)
            {
                Add(
                    "spiPublishedParticipantSampleTest",
                    ("PublishedParticipantSampleTestId", newId()),
                    (PublishedParticipantIdName, participantId),
                    ("SampleReceiptId", assessment.SampleReceiptId),
                    ("PublishedSampleTestId", PublishedId(assessment.SampleTestId)),
                    ("AssessmentComment", assessment.Comment),
                    ("AssessorId", assessment.AssessorId),
                    ("AssessorName", assessment.AssessorName),
                    ("AssessmentDate", assessment.AssessmentDate),
                    ("AssessmentScore", assessment.Score),
                    ("SubmissionDate", assessment.SubmissionDate),
                    ("NonSubmissionDate", assessment.NonSubmissionDate),
                    ("NonCapableDate", assessment.NonCapableDate));
            }

            foreach (var category in participant.CategoryResults)
            {
                var participantCategoryId = newId();
                Add(
                    "spiPublishedParticipantCategoryItem",
                    ("PublishedParticipantCategoryItemId", participantCategoryId),
                    (PublishedParticipantIdName, participantId),
                    ("PublishedCategoryItemId", PublishedId(category.CategoryItemId)),
                    (PublishedSampleIdName, PublishedId(category.SampleId)),
                    ("Score", category.Score));

                foreach (var criterionId in category.CriterionItemIds)
                {
                    Add(
                        "spiPublishedParticipantCriterionItem",
                        ("PublishedParticipantCriterionItemId", newId()),
                        ("PublishedParticipantCategoryItemId", participantCategoryId),
                        ("PublishedCriterionItemId", PublishedId(criterionId)));
                }
            }
        }

        private void SaveAttachments()
        {
            foreach (var attachment in source.Attachments)
            {
                Add(
                    "spiPublishedAttachment",
                    ("PublishedAttachmentId", newId()),
                    (PublishedTabulationIdName, _tabulationId),
                    ("AttachmentId", attachment.AttachmentId),
                    ("FileName", attachment.FileName),
                    ("ContentType", attachment.ContentType),
                    ("FileData", attachment.FileData),
                    ("UploadDate", attachment.UploadDate));
            }
        }
    }
}
